from fastapi import FastAPI, HTTPException, Depends, BackgroundTasks, Request
from fastapi.middleware.cors import CORSMiddleware
from fastapi.responses import JSONResponse
from azure.identity import DefaultAzureCredential
from azure.keyvault.secrets import SecretClient
from azure.servicebus.aio import ServiceBusClient
from azure.monitor.opentelemetry import configure_azure_monitor
import pandas as pd
import numpy as np
from typing import List, Optional, Dict, Any
import asyncio
import redis.asyncio as redis
import json
import os
import logging
from datetime import datetime, timedelta
from contextlib import asynccontextmanager
import time

from app.models.recommendation import RecommendationRequest, RecommendationResponse, FeedbackRequest
from app.services.collaborative_filtering import CollaborativeFilteringEngine
from app.services.content_based import ContentBasedEngine
from app.services.hybrid_recommender import HybridRecommender
from app.api.health import router as health_router

# Configure logging
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(name)s - %(levelname)s - %(message)s'
)
logger = logging.getLogger(__name__)

# Configure Azure Monitor if connection string is available
connection_string = os.getenv("APPLICATIONINSIGHTS_CONNECTION_STRING")
if connection_string:
    configure_azure_monitor(
        connection_string=connection_string,
        enable_live_metrics=True
    )

# Lifespan context manager for startup/shutdown
@asynccontextmanager
async def lifespan(app: FastAPI):
    # Startup
    logger.info("Starting SmartCommerce Recommendation Engine")

    # Initialize Redis client
    redis_host = os.getenv("REDIS_HOST", "localhost")
    redis_port = int(os.getenv("REDIS_PORT", "6379"))
    redis_password = os.getenv("REDIS_PASSWORD")

    app.state.redis_client = redis.Redis(
        host=redis_host,
        port=redis_port,
        password=redis_password,
        decode_responses=True,
        socket_keepalive=True,
        socket_keepalive_options={},
        health_check_interval=30
    )

    # Test Redis connection
    try:
        await app.state.redis_client.ping()
        logger.info("Redis connection established")
    except Exception as e:
        logger.error(f"Failed to connect to Redis: {e}")
        app.state.redis_client = None

    # Initialize ML engines
    logger.info("Initializing ML engines")
    app.state.cf_engine = CollaborativeFilteringEngine()
    app.state.cb_engine = ContentBasedEngine()
    app.state.hybrid_recommender = HybridRecommender(
        app.state.cf_engine,
        app.state.cb_engine
    )

    # Load pre-trained models
    try:
        await load_ml_models(app.state.hybrid_recommender)
        logger.info("ML models loaded successfully")
    except Exception as e:
        logger.error(f"Failed to load ML models: {e}")

    # Initialize Service Bus client
    sb_connection_string = os.getenv("SERVICE_BUS_CONNECTION_STRING")
    if sb_connection_string:
        app.state.service_bus_client = ServiceBusClient.from_connection_string(sb_connection_string)

        # Start event listener
        app.state.event_task = asyncio.create_task(
            event_listener(app.state.service_bus_client)
        )
        logger.info("Service Bus event listener started")
    else:
        logger.warning("Service Bus connection string not provided")
        app.state.service_bus_client = None
        app.state.event_task = None

    yield

    # Shutdown
    logger.info("Shutting down SmartCommerce Recommendation Engine")

    if hasattr(app.state, 'event_task') and app.state.event_task:
        app.state.event_task.cancel()
        try:
            await app.state.event_task
        except asyncio.CancelledError:
            pass

    if hasattr(app.state, 'service_bus_client') and app.state.service_bus_client:
        await app.state.service_bus_client.close()

    if hasattr(app.state, 'redis_client') and app.state.redis_client:
        await app.state.redis_client.close()

app = FastAPI(
    title="SmartCommerce Recommendation Engine",
    description="ML-powered recommendation service with collaborative filtering and content-based algorithms",
    version="1.0.0",
    lifespan=lifespan
)

# CORS configuration
app.add_middleware(
    CORSMiddleware,
    allow_origins=[
        "https://*.azurewebsites.net",
        "https://*.azurecontainerapps.io",
        "http://localhost:3000",
        "http://localhost:8080"
    ],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

# Include health check router
app.include_router(health_router, prefix="/health", tags=["health"])

# Middleware for request timing
@app.middleware("http")
async def add_process_time_header(request: Request, call_next):
    start_time = time.time()
    response = await call_next(request)
    process_time = time.time() - start_time
    response.headers["X-Process-Time"] = str(process_time)
    return response

# Exception handler
@app.exception_handler(Exception)
async def global_exception_handler(request: Request, exc: Exception):
    logger.error(f"Global exception handler caught: {exc}", exc_info=True)
    return JSONResponse(
        status_code=500,
        content={"detail": "Internal server error"}
    )

# Root endpoint
@app.get("/")
async def root():
    return {
        "service": "SmartCommerce Recommendation Engine",
        "version": "1.0.0",
        "status": "running",
        "timestamp": datetime.utcnow().isoformat()
    }

# Get personalized recommendations
@app.post("/api/recommendations", response_model=RecommendationResponse)
async def get_recommendations(
    request: RecommendationRequest,
    background_tasks: BackgroundTasks
):
    """
    Generate personalized product recommendations for a user
    """
    try:
        start_time = time.time()

        # Check cache first
        cache_key = f"recommendations:{request.user_id}:{request.category or 'all'}:{request.count}"
        cached_result = await get_from_cache(cache_key)

        if cached_result and not request.refresh_cache:
            logger.info(f"Returning cached recommendations for user {request.user_id}")
            return RecommendationResponse(**cached_result)

        # Generate recommendations using hybrid approach
        user_profile = await get_user_profile(request.user_id)
        recommendations = await app.state.hybrid_recommender.get_recommendations(
            user_profile,
            count=request.count,
            category=request.category,
            exclude_purchased=request.exclude_purchased
        )

        response = RecommendationResponse(
            user_id=request.user_id,
            recommendations=recommendations,
            generated_at=datetime.utcnow(),
            algorithm_version="hybrid_v1.0",
            cache_ttl=300
        )

        # Cache results
        await set_cache(cache_key, response.dict(), ttl=300)

        # Track recommendation event
        background_tasks.add_task(
            track_recommendation_event,
            request.user_id,
            recommendations,
            time.time() - start_time
        )

        logger.info(f"Generated {len(recommendations)} recommendations for user {request.user_id} in {time.time() - start_time:.3f}s")
        return response

    except Exception as e:
        logger.error(f"Error generating recommendations for user {request.user_id}: {str(e)}")
        raise HTTPException(status_code=500, detail="Failed to generate recommendations")

# Process user feedback
@app.post("/api/feedback")
async def process_feedback(feedback: FeedbackRequest):
    """
    Process user interaction feedback to improve recommendations
    """
    try:
        # Validate feedback data
        if feedback.rating is not None and not (1 <= feedback.rating <= 5):
            raise HTTPException(status_code=400, detail="Rating must be between 1 and 5")

        # Update user interaction matrix
        await update_interaction_matrix(
            feedback.user_id,
            feedback.product_id,
            feedback.action,
            feedback.rating
        )

        # Invalidate user's recommendation cache
        await invalidate_user_cache(feedback.user_id)

        # Trigger incremental model update if needed
        if await should_update_model():
            await app.state.hybrid_recommender.incremental_update()
            logger.info("Triggered incremental model update")

        logger.info(f"Processed {feedback.action} feedback for user {feedback.user_id} on product {feedback.product_id}")
        return {"status": "success", "message": "Feedback processed successfully"}

    except HTTPException:
        raise
    except Exception as e:
        logger.error(f"Error processing feedback: {str(e)}")
        raise HTTPException(status_code=500, detail="Failed to process feedback")

# Get similar products
@app.get("/api/similar/{product_id}")
async def get_similar_products(
    product_id: str,
    count: int = 10,
    background_tasks: BackgroundTasks = BackgroundTasks()
):
    """
    Get products similar to the specified product
    """
    try:
        cache_key = f"similar:{product_id}:{count}"
        cached_result = await get_from_cache(cache_key)

        if cached_result:
            return cached_result

        # Get similar products using content-based filtering
        similar_products = await app.state.cb_engine.get_similar_products(
            product_id,
            count=count
        )

        result = {
            "product_id": product_id,
            "similar_products": similar_products,
            "generated_at": datetime.utcnow().isoformat(),
            "algorithm": "content_based"
        }

        # Cache results
        await set_cache(cache_key, result, ttl=3600)  # Cache for 1 hour

        # Track similar products request
        background_tasks.add_task(
            track_similar_products_event,
            product_id,
            len(similar_products)
        )

        return result

    except Exception as e:
        logger.error(f"Error getting similar products for {product_id}: {str(e)}")
        raise HTTPException(status_code=500, detail="Failed to get similar products")

# Get trending products
@app.get("/api/trending")
async def get_trending_products(
    category: Optional[str] = None,
    count: int = 20,
    time_window: str = "24h"
):
    """
    Get trending products based on recent user interactions
    """
    try:
        cache_key = f"trending:{category or 'all'}:{count}:{time_window}"
        cached_result = await get_from_cache(cache_key)

        if cached_result:
            return cached_result

        # Calculate trending products
        trending_products = await calculate_trending_products(
            category=category,
            count=count,
            time_window=time_window
        )

        result = {
            "category": category,
            "time_window": time_window,
            "trending_products": trending_products,
            "generated_at": datetime.utcnow().isoformat()
        }

        # Cache results
        await set_cache(cache_key, result, ttl=1800)  # Cache for 30 minutes

        return result

    except Exception as e:
        logger.error(f"Error getting trending products: {str(e)}")
        raise HTTPException(status_code=500, detail="Failed to get trending products")

# Utility functions
async def get_from_cache(key: str) -> Optional[Dict[str, Any]]:
    """Get data from Redis cache"""
    if not hasattr(app.state, 'redis_client') or not app.state.redis_client:
        return None

    try:
        cached_data = await app.state.redis_client.get(key)
        if cached_data:
            return json.loads(cached_data)
    except Exception as e:
        logger.warning(f"Cache get error for key {key}: {e}")

    return None

async def set_cache(key: str, data: Dict[str, Any], ttl: int = 300):
    """Set data in Redis cache"""
    if not hasattr(app.state, 'redis_client') or not app.state.redis_client:
        return

    try:
        await app.state.redis_client.setex(
            key,
            ttl,
            json.dumps(data, default=str)
        )
    except Exception as e:
        logger.warning(f"Cache set error for key {key}: {e}")

async def invalidate_user_cache(user_id: str):
    """Invalidate all cache entries for a user"""
    if not hasattr(app.state, 'redis_client') or not app.state.redis_client:
        return

    try:
        pattern = f"recommendations:{user_id}:*"
        async for key in app.state.redis_client.scan_iter(match=pattern):
            await app.state.redis_client.delete(key)
    except Exception as e:
        logger.warning(f"Cache invalidation error for user {user_id}: {e}")

async def get_user_profile(user_id: str) -> Dict[str, Any]:
    """Get user profile and interaction history"""
    # In a real implementation, this would fetch from the user service
    # For now, return a mock profile
    return {
        "user_id": user_id,
        "preferences": [],
        "purchase_history": [],
        "view_history": [],
        "demographics": {}
    }

async def update_interaction_matrix(user_id: str, product_id: str, action: str, rating: Optional[float] = None):
    """Update the user-item interaction matrix"""
    # In a real implementation, this would update the ML model's training data
    logger.info(f"Updating interaction matrix: user={user_id}, product={product_id}, action={action}, rating={rating}")

async def should_update_model() -> bool:
    """Determine if the model should be updated based on new interactions"""
    # Simple implementation: update every 100 interactions
    # In practice, this would be more sophisticated
    return False

async def load_ml_models(hybrid_recommender):
    """Load pre-trained ML models"""
    # In a real implementation, this would load models from blob storage
    logger.info("Loading ML models (mock implementation)")
    await asyncio.sleep(0.1)  # Simulate loading time

async def calculate_trending_products(category: Optional[str], count: int, time_window: str) -> List[Dict[str, Any]]:
    """Calculate trending products based on recent interactions"""
    # Mock implementation
    trending = [
        {
            "product_id": f"trending_{i}",
            "name": f"Trending Product {i}",
            "score": 100 - i,
            "category": category or "electronics"
        }
        for i in range(1, count + 1)
    ]
    return trending

async def track_recommendation_event(user_id: str, recommendations: List[Dict], duration: float):
    """Track recommendation generation event"""
    logger.info(f"Tracked recommendation event: user={user_id}, count={len(recommendations)}, duration={duration:.3f}s")

async def track_similar_products_event(product_id: str, count: int):
    """Track similar products request event"""
    logger.info(f"Tracked similar products event: product={product_id}, count={count}")

# Event listener for Service Bus messages
async def event_listener(service_bus_client):
    """Listen for events from other services"""
    if not service_bus_client:
        return

    try:
        async with service_bus_client:
            receiver = service_bus_client.get_queue_receiver(queue_name="order-events")
            async with receiver:
                async for message in receiver:
                    try:
                        event_data = json.loads(str(message))
                        await process_order_event(event_data)
                        await receiver.complete_message(message)
                        logger.info(f"Processed message {message.message_id}")
                    except Exception as e:
                        logger.error(f"Error processing message {message.message_id}: {str(e)}")
                        await receiver.abandon_message(message)
    except Exception as e:
        logger.error(f"Error in event listener: {str(e)}")

async def process_order_event(event_data: Dict[str, Any]):
    """Process order-related events to update recommendations"""
    event_type = event_data.get("eventType", "")

    if event_type == "OrderCreated":
        user_id = event_data.get("customerId")
        items = event_data.get("items", [])

        # Update interaction matrix with purchase events
        for item in items:
            await update_interaction_matrix(
                user_id,
                item["productId"],
                "purchase",
                rating=5.0  # Implicit high rating for purchases
            )

        # Invalidate user's recommendation cache
        await invalidate_user_cache(user_id)

        logger.info(f"Processed order created event for user {user_id} with {len(items)} items")

if __name__ == "__main__":
    import uvicorn

    port = int(os.getenv("PORT", "8000"))
    host = os.getenv("HOST", "0.0.0.0")

    uvicorn.run(
        "main:app",
        host=host,
        port=port,
        reload=False,
        log_level="info"
    )