"""
SmartCommerce Search Service

This service provides intelligent search capabilities including semantic search,
natural language processing, personalized search results, and advanced analytics.
It supports multiple search backends and implements state-of-the-art NLP techniques.
"""

import asyncio
import logging
from contextlib import asynccontextmanager
from typing import Dict, Any, List, Optional

import structlog
import uvicorn
from fastapi import FastAPI, HTTPException, BackgroundTasks, Depends, Query
from fastapi.middleware.cors import CORSMiddleware
from fastapi.responses import JSONResponse
from prometheus_client import Counter, Histogram, generate_latest, CONTENT_TYPE_LATEST

from app.core.config import get_settings
from app.core.logging import setup_logging
from app.core.database import init_db, close_db
from app.core.redis_client import init_redis, close_redis
from app.core.service_bus import init_service_bus, close_service_bus
from app.api.routes import search, analytics, indexing, health
from app.services.search_engine import SearchEngineService
from app.services.semantic_search import SemanticSearchService
from app.services.personalization_engine import PersonalizationEngineService
from app.services.search_analytics import SearchAnalyticsService
from app.services.index_manager import IndexManagerService
from app.models.schemas import (
    SearchRequest,
    SearchResponse,
    SemanticSearchRequest,
    PersonalizedSearchRequest,
    IndexRequest,
    SearchAnalyticsRequest,
    AutocompleteRequest
)

# Metrics
SEARCH_REQUESTS = Counter('search_requests_total', 'Total search requests', ['search_type'])
SEARCH_LATENCY = Histogram('search_latency_seconds', 'Search request latency')
SEARCH_RESULTS = Counter('search_results_total', 'Total search results returned')
ZERO_RESULTS = Counter('search_zero_results_total', 'Searches with zero results')

settings = get_settings()
logger = structlog.get_logger()


@asynccontextmanager
async def lifespan(app: FastAPI):
    """Application lifespan manager"""
    logger.info("Starting Search Service")

    # Initialize services
    await init_db()
    await init_redis()
    await init_service_bus()

    # Initialize search services
    search_engine = SearchEngineService()
    semantic_search = SemanticSearchService()
    personalization_engine = PersonalizationEngineService()
    search_analytics = SearchAnalyticsService()
    index_manager = IndexManagerService()

    # Load models and initialize indices
    await search_engine.initialize()
    await semantic_search.load_models()
    await personalization_engine.load_user_models()
    await search_analytics.initialize_analytics()
    await index_manager.initialize_indices()

    # Store services in app state
    app.state.search_engine = search_engine
    app.state.semantic_search = semantic_search
    app.state.personalization_engine = personalization_engine
    app.state.search_analytics = search_analytics
    app.state.index_manager = index_manager

    # Start background tasks
    background_task = asyncio.create_task(start_background_tasks(app))

    logger.info("Search Service started successfully")

    yield

    # Cleanup
    logger.info("Shutting down Search Service")
    background_task.cancel()
    try:
        await background_task
    except asyncio.CancelledError:
        pass

    await close_service_bus()
    await close_redis()
    await close_db()
    logger.info("Search Service stopped")


async def start_background_tasks(app: FastAPI):
    """Start background maintenance tasks"""
    try:
        while True:
            # Update search indices
            index_manager: IndexManagerService = app.state.index_manager
            await index_manager.update_indices()

            # Update personalization models
            personalization_engine: PersonalizationEngineService = app.state.personalization_engine
            await personalization_engine.update_user_profiles()

            # Clean up old analytics data
            search_analytics: SearchAnalyticsService = app.state.search_analytics
            await search_analytics.cleanup_old_data()

            # Sleep for maintenance interval
            await asyncio.sleep(settings.INDEX_UPDATE_INTERVAL_SECONDS)

    except asyncio.CancelledError:
        logger.info("Background tasks cancelled")
    except Exception as e:
        logger.error("Background tasks error", error=str(e))


def create_app() -> FastAPI:
    """Create and configure FastAPI application"""

    # Setup logging
    setup_logging()

    app = FastAPI(
        title="SmartCommerce Search Service",
        description="Intelligent search with NLP, semantic search, and personalization",
        version="1.0.0",
        lifespan=lifespan,
        docs_url="/docs" if settings.ENVIRONMENT == "development" else None,
        redoc_url="/redoc" if settings.ENVIRONMENT == "development" else None,
    )

    # CORS
    app.add_middleware(
        CORSMiddleware,
        allow_origins=settings.ALLOWED_ORIGINS,
        allow_credentials=True,
        allow_methods=["*"],
        allow_headers=["*"],
    )

    # Request logging middleware
    @app.middleware("http")
    async def log_requests(request, call_next):
        start_time = asyncio.get_event_loop().time()

        response = await call_next(request)

        duration = asyncio.get_event_loop().time() - start_time
        SEARCH_LATENCY.observe(duration)

        logger.info(
            "Request processed",
            method=request.method,
            path=request.url.path,
            status_code=response.status_code,
            duration=duration
        )

        return response

    # Exception handler
    @app.exception_handler(Exception)
    async def global_exception_handler(request, exc):
        logger.error(
            "Unhandled exception",
            error=str(exc),
            path=request.url.path,
            method=request.method,
            exc_info=True
        )
        return JSONResponse(
            status_code=500,
            content={"detail": "Internal server error"}
        )

    # Include routers
    app.include_router(health.router, prefix="/health", tags=["health"])
    app.include_router(search.router, prefix="/api/v1/search", tags=["search"])
    app.include_router(analytics.router, prefix="/api/v1/analytics", tags=["analytics"])
    app.include_router(indexing.router, prefix="/api/v1/indexing", tags=["indexing"])

    # Core search endpoints
    @app.post("/api/v1/search", response_model=SearchResponse)
    async def search_products(
        request: SearchRequest,
        background_tasks: BackgroundTasks
    ):
        """Execute product search with various algorithms"""
        try:
            SEARCH_REQUESTS.labels(search_type="standard").inc()

            search_engine: SearchEngineService = app.state.search_engine
            search_analytics: SearchAnalyticsService = app.state.search_analytics

            # Execute search
            results = await search_engine.search(
                query=request.query,
                filters=request.filters,
                sort_options=request.sort_options,
                pagination=request.pagination,
                search_options=request.options
            )

            # Track search metrics
            SEARCH_RESULTS.inc(len(results.products))
            if len(results.products) == 0:
                ZERO_RESULTS.inc()

            # Log search analytics in background
            background_tasks.add_task(
                log_search_analytics,
                request.query,
                len(results.products),
                request.user_id
            )

            return results

        except Exception as e:
            logger.error("Search failed", error=str(e), query=request.query)
            raise HTTPException(status_code=500, detail="Search failed")

    @app.post("/api/v1/semantic-search", response_model=SearchResponse)
    async def semantic_search(
        request: SemanticSearchRequest,
        background_tasks: BackgroundTasks
    ):
        """Execute semantic search using NLP models"""
        try:
            SEARCH_REQUESTS.labels(search_type="semantic").inc()

            semantic_search: SemanticSearchService = app.state.semantic_search

            results = await semantic_search.semantic_search(
                query=request.query,
                search_type=request.search_type,
                similarity_threshold=request.similarity_threshold,
                max_results=request.max_results,
                filters=request.filters
            )

            # Track semantic search usage
            background_tasks.add_task(
                track_semantic_search,
                request.query,
                request.search_type,
                len(results.products)
            )

            return results

        except Exception as e:
            logger.error("Semantic search failed", error=str(e), query=request.query)
            raise HTTPException(status_code=500, detail="Semantic search failed")

    @app.post("/api/v1/personalized-search", response_model=SearchResponse)
    async def personalized_search(
        request: PersonalizedSearchRequest,
        background_tasks: BackgroundTasks
    ):
        """Execute personalized search based on user behavior"""
        try:
            SEARCH_REQUESTS.labels(search_type="personalized").inc()

            personalization_engine: PersonalizationEngineService = app.state.personalization_engine
            search_engine: SearchEngineService = app.state.search_engine

            # Get base search results
            base_results = await search_engine.search(
                query=request.query,
                filters=request.filters,
                sort_options=request.sort_options,
                pagination=request.pagination
            )

            # Apply personalization
            personalized_results = await personalization_engine.personalize_results(
                user_id=request.user_id,
                base_results=base_results,
                personalization_strength=request.personalization_strength,
                user_context=request.user_context
            )

            # Update user profile in background
            background_tasks.add_task(
                update_user_search_profile,
                request.user_id,
                request.query,
                personalized_results
            )

            return personalized_results

        except Exception as e:
            logger.error("Personalized search failed", error=str(e),
                        query=request.query, user_id=request.user_id)
            raise HTTPException(status_code=500, detail="Personalized search failed")

    @app.get("/api/v1/autocomplete")
    async def get_autocomplete_suggestions(
        q: str = Query(..., description="Query prefix"),
        user_id: Optional[str] = Query(None, description="User ID for personalization"),
        max_suggestions: int = Query(10, description="Maximum number of suggestions")
    ):
        """Get autocomplete suggestions for search queries"""
        try:
            search_engine: SearchEngineService = app.state.search_engine

            suggestions = await search_engine.get_autocomplete_suggestions(
                query_prefix=q,
                user_id=user_id,
                max_suggestions=max_suggestions
            )

            return {"suggestions": suggestions}

        except Exception as e:
            logger.error("Autocomplete failed", error=str(e), query=q)
            raise HTTPException(status_code=500, detail="Autocomplete failed")

    @app.get("/api/v1/trending-searches")
    async def get_trending_searches(
        timeframe: str = Query("24h", description="Timeframe (1h, 24h, 7d, 30d)"),
        category: Optional[str] = Query(None, description="Product category filter"),
        limit: int = Query(10, description="Number of trending searches")
    ):
        """Get trending search terms"""
        try:
            search_analytics: SearchAnalyticsService = app.state.search_analytics

            trending = await search_analytics.get_trending_searches(
                timeframe=timeframe,
                category=category,
                limit=limit
            )

            return {"trending_searches": trending}

        except Exception as e:
            logger.error("Failed to get trending searches", error=str(e))
            raise HTTPException(status_code=500, detail="Failed to get trending searches")

    @app.post("/api/v1/index-product")
    async def index_product(
        request: IndexRequest,
        background_tasks: BackgroundTasks
    ):
        """Index a product for search"""
        try:
            index_manager: IndexManagerService = app.state.index_manager

            result = await index_manager.index_product(
                product_data=request.product_data,
                index_options=request.options
            )

            # Update related indices in background
            background_tasks.add_task(
                update_related_indices,
                request.product_data
            )

            return {"indexed": True, "product_id": request.product_data.get("id")}

        except Exception as e:
            logger.error("Product indexing failed", error=str(e))
            raise HTTPException(status_code=500, detail="Product indexing failed")

    @app.delete("/api/v1/index-product/{product_id}")
    async def remove_product_from_index(
        product_id: str,
        background_tasks: BackgroundTasks
    ):
        """Remove a product from search index"""
        try:
            index_manager: IndexManagerService = app.state.index_manager

            result = await index_manager.remove_product(product_id)

            # Clean up related data in background
            background_tasks.add_task(
                cleanup_product_data,
                product_id
            )

            return {"removed": result, "product_id": product_id}

        except Exception as e:
            logger.error("Product removal failed", error=str(e), product_id=product_id)
            raise HTTPException(status_code=500, detail="Product removal failed")

    @app.get("/api/v1/search-suggestions/{user_id}")
    async def get_search_suggestions(user_id: str):
        """Get personalized search suggestions for a user"""
        try:
            personalization_engine: PersonalizationEngineService = app.state.personalization_engine

            suggestions = await personalization_engine.get_user_search_suggestions(
                user_id=user_id
            )

            return {"suggestions": suggestions}

        except Exception as e:
            logger.error("Failed to get search suggestions", error=str(e), user_id=user_id)
            raise HTTPException(status_code=500, detail="Failed to get search suggestions")

    @app.get("/api/v1/search-analytics")
    async def get_search_analytics(
        timeframe: str = Query("24h", description="Analytics timeframe"),
        user_id: Optional[str] = Query(None, description="User ID filter")
    ):
        """Get search analytics and insights"""
        try:
            search_analytics: SearchAnalyticsService = app.state.search_analytics

            analytics = await search_analytics.get_search_analytics(
                timeframe=timeframe,
                user_id=user_id
            )

            return analytics

        except Exception as e:
            logger.error("Failed to get search analytics", error=str(e))
            raise HTTPException(status_code=500, detail="Failed to get search analytics")

    @app.post("/api/v1/track-search-interaction")
    async def track_search_interaction(
        query: str,
        user_id: Optional[str] = None,
        product_id: Optional[str] = None,
        interaction_type: str = "click",
        position: Optional[int] = None
    ):
        """Track user interaction with search results"""
        try:
            search_analytics: SearchAnalyticsService = app.state.search_analytics

            await search_analytics.track_interaction(
                query=query,
                user_id=user_id,
                product_id=product_id,
                interaction_type=interaction_type,
                position=position
            )

            return {"tracked": True}

        except Exception as e:
            logger.error("Failed to track search interaction", error=str(e))
            raise HTTPException(status_code=500, detail="Failed to track interaction")

    @app.get("/api/v1/search-health")
    async def get_search_health():
        """Get search service health and performance metrics"""
        try:
            search_engine: SearchEngineService = app.state.search_engine
            index_manager: IndexManagerService = app.state.index_manager

            search_health = await search_engine.get_health_status()
            index_health = await index_manager.get_index_health()

            return {
                "search_engine": search_health,
                "index_manager": index_health,
                "overall_status": "healthy" if search_health["status"] == "healthy" and
                                               index_health["status"] == "healthy" else "degraded"
            }

        except Exception as e:
            logger.error("Failed to get search health", error=str(e))
            raise HTTPException(status_code=500, detail="Failed to get search health")

    # Metrics endpoint
    @app.get("/metrics")
    async def get_metrics():
        """Prometheus metrics endpoint"""
        return generate_latest()

    return app


async def log_search_analytics(query: str, result_count: int, user_id: Optional[str]):
    """Log search analytics for analysis"""
    try:
        logger.info(
            "Search analytics",
            query=query,
            result_count=result_count,
            user_id=user_id
        )

        # In a real implementation, this would:
        # 1. Store search query and results
        # 2. Update search popularity metrics
        # 3. Track zero-result queries
        # 4. Update user search history

    except Exception as e:
        logger.error("Failed to log search analytics", error=str(e))


async def track_semantic_search(query: str, search_type: str, result_count: int):
    """Track semantic search usage"""
    try:
        logger.info(
            "Semantic search tracked",
            query=query,
            search_type=search_type,
            result_count=result_count
        )

        # In a real implementation, this would:
        # 1. Update semantic search metrics
        # 2. Improve model performance tracking
        # 3. Identify query patterns

    except Exception as e:
        logger.error("Failed to track semantic search", error=str(e))


async def update_user_search_profile(user_id: str, query: str, results: SearchResponse):
    """Update user search profile for personalization"""
    try:
        logger.info(
            "Updating user search profile",
            user_id=user_id,
            query=query
        )

        # In a real implementation, this would:
        # 1. Update user interest vectors
        # 2. Track search patterns
        # 3. Improve personalization models

    except Exception as e:
        logger.error("Failed to update user search profile", error=str(e))


async def update_related_indices(product_data: Dict[str, Any]):
    """Update related search indices"""
    try:
        logger.info(
            "Updating related indices",
            product_id=product_data.get("id")
        )

        # In a real implementation, this would:
        # 1. Update category indices
        # 2. Update brand indices
        # 3. Update recommendation indices

    except Exception as e:
        logger.error("Failed to update related indices", error=str(e))


async def cleanup_product_data(product_id: str):
    """Clean up product-related data"""
    try:
        logger.info(
            "Cleaning up product data",
            product_id=product_id
        )

        # In a real implementation, this would:
        # 1. Remove from all indices
        # 2. Clean up analytics data
        # 3. Update recommendation models

    except Exception as e:
        logger.error("Failed to cleanup product data", error=str(e))


# Create the FastAPI app
app = create_app()

if __name__ == "__main__":
    uvicorn.run(
        "app.main:app",
        host="0.0.0.0",
        port=8000,
        reload=settings.ENVIRONMENT == "development",
        log_config=None  # Use our custom logging
    )