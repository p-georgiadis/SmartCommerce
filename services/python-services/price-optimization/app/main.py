"""
SmartCommerce Price Optimization Service

This service provides dynamic pricing optimization using machine learning algorithms.
It analyzes market data, competitor prices, demand patterns, and inventory levels
to recommend optimal pricing strategies.
"""

import asyncio
import logging
from contextlib import asynccontextmanager
from typing import Dict, Any

import structlog
import uvicorn
from fastapi import FastAPI, HTTPException, Depends, BackgroundTasks
from fastapi.middleware.cors import CORSMiddleware
from fastapi.responses import JSONResponse
from prometheus_client import Counter, Histogram, generate_latest, CONTENT_TYPE_LATEST

from app.core.config import get_settings
from app.core.logging import setup_logging
from app.core.database import init_db, close_db
from app.core.redis_client import init_redis, close_redis
from app.core.service_bus import init_service_bus, close_service_bus
from app.api.routes import pricing, analytics, health
from app.services.price_optimizer import PriceOptimizerService
from app.services.market_analyzer import MarketAnalyzerService
from app.services.demand_forecaster import DemandForecasterService
from app.models.schemas import (
    PriceOptimizationRequest,
    PriceOptimizationResponse,
    MarketAnalysisRequest,
    BulkPriceOptimizationRequest
)

# Metrics
REQUEST_COUNT = Counter('price_optimization_requests_total', 'Total price optimization requests', ['method', 'endpoint'])
REQUEST_DURATION = Histogram('price_optimization_request_duration_seconds', 'Request duration')
OPTIMIZATION_COUNT = Counter('price_optimizations_total', 'Total price optimizations performed', ['product_category'])

settings = get_settings()
logger = structlog.get_logger()


@asynccontextmanager
async def lifespan(app: FastAPI):
    """Application lifespan manager"""
    logger.info("Starting Price Optimization Service")

    # Initialize services
    await init_db()
    await init_redis()
    await init_service_bus()

    # Initialize ML models
    price_optimizer = PriceOptimizerService()
    market_analyzer = MarketAnalyzerService()
    demand_forecaster = DemandForecasterService()

    # Load pre-trained models
    await price_optimizer.load_models()
    await market_analyzer.load_models()
    await demand_forecaster.load_models()

    # Store services in app state
    app.state.price_optimizer = price_optimizer
    app.state.market_analyzer = market_analyzer
    app.state.demand_forecaster = demand_forecaster

    logger.info("Price Optimization Service started successfully")

    yield

    # Cleanup
    logger.info("Shutting down Price Optimization Service")
    await close_service_bus()
    await close_redis()
    await close_db()
    logger.info("Price Optimization Service stopped")


def create_app() -> FastAPI:
    """Create and configure FastAPI application"""

    # Setup logging
    setup_logging()

    app = FastAPI(
        title="SmartCommerce Price Optimization Service",
        description="Dynamic pricing optimization using machine learning algorithms",
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

        REQUEST_COUNT.labels(
            method=request.method,
            endpoint=request.url.path
        ).inc()

        response = await call_next(request)

        duration = asyncio.get_event_loop().time() - start_time
        REQUEST_DURATION.observe(duration)

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
    app.include_router(pricing.router, prefix="/api/v1/pricing", tags=["pricing"])
    app.include_router(analytics.router, prefix="/api/v1/analytics", tags=["analytics"])

    # Core pricing endpoints
    @app.post("/api/v1/optimize-price", response_model=PriceOptimizationResponse)
    async def optimize_price(
        request: PriceOptimizationRequest,
        background_tasks: BackgroundTasks
    ):
        """Optimize price for a single product"""
        try:
            price_optimizer: PriceOptimizerService = app.state.price_optimizer

            result = await price_optimizer.optimize_product_price(
                product_id=request.product_id,
                current_price=request.current_price,
                cost=request.cost,
                inventory_level=request.inventory_level,
                competitor_prices=request.competitor_prices,
                demand_data=request.demand_data,
                constraints=request.constraints
            )

            OPTIMIZATION_COUNT.labels(
                product_category=request.category or "unknown"
            ).inc()

            # Schedule price update in background
            if request.auto_apply:
                background_tasks.add_task(
                    apply_price_optimization,
                    request.product_id,
                    result.recommended_price
                )

            return result

        except Exception as e:
            logger.error("Price optimization failed", error=str(e), product_id=request.product_id)
            raise HTTPException(status_code=500, detail="Price optimization failed")

    @app.post("/api/v1/bulk-optimize", response_model=Dict[str, PriceOptimizationResponse])
    async def bulk_optimize_prices(
        request: BulkPriceOptimizationRequest,
        background_tasks: BackgroundTasks
    ):
        """Optimize prices for multiple products"""
        try:
            price_optimizer: PriceOptimizerService = app.state.price_optimizer

            results = await price_optimizer.bulk_optimize_prices(
                products=request.products,
                market_conditions=request.market_conditions,
                constraints=request.global_constraints
            )

            # Schedule bulk price updates if requested
            if request.auto_apply:
                background_tasks.add_task(
                    apply_bulk_price_optimization,
                    results
                )

            return results

        except Exception as e:
            logger.error("Bulk price optimization failed", error=str(e))
            raise HTTPException(status_code=500, detail="Bulk price optimization failed")

    @app.post("/api/v1/analyze-market")
    async def analyze_market(request: MarketAnalysisRequest):
        """Analyze market conditions for pricing decisions"""
        try:
            market_analyzer: MarketAnalyzerService = app.state.market_analyzer

            analysis = await market_analyzer.analyze_market(
                category=request.category,
                timeframe=request.timeframe,
                competitors=request.competitors
            )

            return analysis

        except Exception as e:
            logger.error("Market analysis failed", error=str(e))
            raise HTTPException(status_code=500, detail="Market analysis failed")

    @app.get("/api/v1/pricing-insights/{product_id}")
    async def get_pricing_insights(product_id: str):
        """Get comprehensive pricing insights for a product"""
        try:
            price_optimizer: PriceOptimizerService = app.state.price_optimizer
            market_analyzer: MarketAnalyzerService = app.state.market_analyzer
            demand_forecaster: DemandForecasterService = app.state.demand_forecaster

            # Get insights from all services
            price_insights = await price_optimizer.get_pricing_insights(product_id)
            market_insights = await market_analyzer.get_product_market_position(product_id)
            demand_forecast = await demand_forecaster.forecast_demand(product_id)

            return {
                "product_id": product_id,
                "price_insights": price_insights,
                "market_insights": market_insights,
                "demand_forecast": demand_forecast,
                "generated_at": asyncio.get_event_loop().time()
            }

        except Exception as e:
            logger.error("Failed to get pricing insights", error=str(e), product_id=product_id)
            raise HTTPException(status_code=500, detail="Failed to get pricing insights")

    # Metrics endpoint
    @app.get("/metrics")
    async def get_metrics():
        """Prometheus metrics endpoint"""
        return generate_latest()

    return app


async def apply_price_optimization(product_id: str, new_price: float):
    """Apply price optimization result"""
    try:
        # In a real implementation, this would call the catalog service
        # to update the product price
        logger.info(
            "Applying price optimization",
            product_id=product_id,
            new_price=new_price
        )

        # Simulate API call to catalog service
        await asyncio.sleep(0.1)

        logger.info(
            "Price optimization applied successfully",
            product_id=product_id,
            new_price=new_price
        )

    except Exception as e:
        logger.error(
            "Failed to apply price optimization",
            error=str(e),
            product_id=product_id,
            new_price=new_price
        )


async def apply_bulk_price_optimization(results: Dict[str, PriceOptimizationResponse]):
    """Apply bulk price optimization results"""
    try:
        for product_id, result in results.items():
            await apply_price_optimization(product_id, result.recommended_price)

        logger.info(
            "Bulk price optimization applied",
            products_count=len(results)
        )

    except Exception as e:
        logger.error(
            "Failed to apply bulk price optimization",
            error=str(e),
            products_count=len(results)
        )


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