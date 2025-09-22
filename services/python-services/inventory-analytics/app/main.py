"""
SmartCommerce Inventory Analytics Service

This service provides advanced inventory analytics including demand forecasting,
stock optimization, supply chain analytics, and intelligent reorder recommendations
using machine learning and optimization algorithms.
"""

import asyncio
import logging
from contextlib import asynccontextmanager
from typing import Dict, Any, List

import structlog
import uvicorn
from fastapi import FastAPI, HTTPException, BackgroundTasks, Depends
from fastapi.middleware.cors import CORSMiddleware
from fastapi.responses import JSONResponse
from prometheus_client import Counter, Histogram, generate_latest, CONTENT_TYPE_LATEST

from app.core.config import get_settings
from app.core.logging import setup_logging
from app.core.database import init_db, close_db
from app.core.redis_client import init_redis, close_redis
from app.core.service_bus import init_service_bus, close_service_bus
from app.api.routes import inventory, forecasting, optimization, health
from app.services.demand_forecaster import DemandForecastingService
from app.services.inventory_optimizer import InventoryOptimizationService
from app.services.supply_chain_analyzer import SupplyChainAnalyzerService
from app.services.reorder_manager import ReorderManagerService
from app.models.schemas import (
    DemandForecastRequest,
    DemandForecastResponse,
    InventoryOptimizationRequest,
    StockReorderRequest,
    SupplyChainAnalysisRequest,
    InventoryAlertRequest
)

# Metrics
FORECAST_REQUESTS = Counter('inventory_forecast_requests_total', 'Total demand forecast requests')
OPTIMIZATION_REQUESTS = Counter('inventory_optimization_requests_total', 'Total optimization requests')
PROCESSING_TIME = Histogram('inventory_analytics_processing_seconds', 'Analytics processing time')
STOCK_ALERTS = Counter('inventory_stock_alerts_total', 'Stock alerts generated', ['alert_type'])

settings = get_settings()
logger = structlog.get_logger()


@asynccontextmanager
async def lifespan(app: FastAPI):
    """Application lifespan manager"""
    logger.info("Starting Inventory Analytics Service")

    # Initialize services
    await init_db()
    await init_redis()
    await init_service_bus()

    # Initialize analytics services
    demand_forecaster = DemandForecastingService()
    inventory_optimizer = InventoryOptimizationService()
    supply_chain_analyzer = SupplyChainAnalyzerService()
    reorder_manager = ReorderManagerService()

    # Load ML models and historical data
    await demand_forecaster.load_models()
    await inventory_optimizer.load_optimization_models()
    await supply_chain_analyzer.load_supply_chain_data()
    await reorder_manager.initialize_reorder_rules()

    # Store services in app state
    app.state.demand_forecaster = demand_forecaster
    app.state.inventory_optimizer = inventory_optimizer
    app.state.supply_chain_analyzer = supply_chain_analyzer
    app.state.reorder_manager = reorder_manager

    # Start background monitoring
    background_monitor_task = asyncio.create_task(start_background_monitoring(app))

    logger.info("Inventory Analytics Service started successfully")

    yield

    # Cleanup
    logger.info("Shutting down Inventory Analytics Service")
    background_monitor_task.cancel()
    try:
        await background_monitor_task
    except asyncio.CancelledError:
        pass

    await close_service_bus()
    await close_redis()
    await close_db()
    logger.info("Inventory Analytics Service stopped")


async def start_background_monitoring(app: FastAPI):
    """Start background monitoring tasks"""
    try:
        while True:
            # Run periodic inventory checks
            reorder_manager: ReorderManagerService = app.state.reorder_manager
            await reorder_manager.check_reorder_points()

            # Update demand forecasts
            demand_forecaster: DemandForecastingService = app.state.demand_forecaster
            await demand_forecaster.update_forecasts()

            # Sleep for monitoring interval
            await asyncio.sleep(settings.MONITORING_INTERVAL_SECONDS)

    except asyncio.CancelledError:
        logger.info("Background monitoring cancelled")
    except Exception as e:
        logger.error("Background monitoring error", error=str(e))


def create_app() -> FastAPI:
    """Create and configure FastAPI application"""

    # Setup logging
    setup_logging()

    app = FastAPI(
        title="SmartCommerce Inventory Analytics Service",
        description="Advanced inventory analytics with demand forecasting and optimization",
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
        PROCESSING_TIME.observe(duration)

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
    app.include_router(inventory.router, prefix="/api/v1/inventory", tags=["inventory"])
    app.include_router(forecasting.router, prefix="/api/v1/forecasting", tags=["forecasting"])
    app.include_router(optimization.router, prefix="/api/v1/optimization", tags=["optimization"])

    # Core inventory analytics endpoints
    @app.post("/api/v1/forecast-demand", response_model=DemandForecastResponse)
    async def forecast_demand(
        request: DemandForecastRequest,
        background_tasks: BackgroundTasks
    ):
        """Generate demand forecast for products"""
        try:
            FORECAST_REQUESTS.inc()

            demand_forecaster: DemandForecastingService = app.state.demand_forecaster

            forecast = await demand_forecaster.generate_forecast(
                product_ids=request.product_ids,
                forecast_horizon=request.forecast_horizon,
                granularity=request.granularity,
                include_seasonality=request.include_seasonality,
                include_promotions=request.include_promotions,
                external_factors=request.external_factors
            )

            # Schedule forecast accuracy tracking
            background_tasks.add_task(
                track_forecast_accuracy,
                request.product_ids,
                forecast
            )

            return forecast

        except Exception as e:
            logger.error("Demand forecasting failed", error=str(e))
            raise HTTPException(status_code=500, detail="Demand forecasting failed")

    @app.post("/api/v1/optimize-inventory")
    async def optimize_inventory(
        request: InventoryOptimizationRequest,
        background_tasks: BackgroundTasks
    ):
        """Optimize inventory levels and reorder points"""
        try:
            OPTIMIZATION_REQUESTS.inc()

            inventory_optimizer: InventoryOptimizationService = app.state.inventory_optimizer

            optimization = await inventory_optimizer.optimize_inventory(
                products=request.products,
                constraints=request.constraints,
                objectives=request.objectives,
                time_horizon=request.time_horizon
            )

            # Apply optimization recommendations if auto-apply is enabled
            if request.auto_apply:
                background_tasks.add_task(
                    apply_optimization_recommendations,
                    optimization.recommendations
                )

            return optimization

        except Exception as e:
            logger.error("Inventory optimization failed", error=str(e))
            raise HTTPException(status_code=500, detail="Inventory optimization failed")

    @app.post("/api/v1/analyze-supply-chain")
    async def analyze_supply_chain(request: SupplyChainAnalysisRequest):
        """Analyze supply chain performance and risks"""
        try:
            supply_chain_analyzer: SupplyChainAnalyzerService = app.state.supply_chain_analyzer

            analysis = await supply_chain_analyzer.analyze_supply_chain(
                scope=request.scope,
                timeframe=request.timeframe,
                include_risk_assessment=request.include_risk_assessment,
                include_performance_metrics=request.include_performance_metrics
            )

            return analysis

        except Exception as e:
            logger.error("Supply chain analysis failed", error=str(e))
            raise HTTPException(status_code=500, detail="Supply chain analysis failed")

    @app.post("/api/v1/generate-reorder-recommendations")
    async def generate_reorder_recommendations(
        request: StockReorderRequest,
        background_tasks: BackgroundTasks
    ):
        """Generate intelligent reorder recommendations"""
        try:
            reorder_manager: ReorderManagerService = app.state.reorder_manager

            recommendations = await reorder_manager.generate_reorder_recommendations(
                product_ids=request.product_ids,
                include_demand_forecast=request.include_demand_forecast,
                include_supplier_analysis=request.include_supplier_analysis,
                urgency_threshold=request.urgency_threshold
            )

            # Auto-create purchase orders if enabled
            if request.auto_create_orders:
                background_tasks.add_task(
                    create_purchase_orders,
                    recommendations
                )

            return {"recommendations": recommendations}

        except Exception as e:
            logger.error("Reorder recommendation failed", error=str(e))
            raise HTTPException(status_code=500, detail="Reorder recommendation failed")

    @app.get("/api/v1/inventory-insights/{product_id}")
    async def get_inventory_insights(product_id: str):
        """Get comprehensive inventory insights for a product"""
        try:
            demand_forecaster: DemandForecastingService = app.state.demand_forecaster
            inventory_optimizer: InventoryOptimizationService = app.state.inventory_optimizer
            supply_chain_analyzer: SupplyChainAnalyzerService = app.state.supply_chain_analyzer

            # Get insights from all services
            demand_insights = await demand_forecaster.get_product_demand_insights(product_id)
            optimization_insights = await inventory_optimizer.get_product_optimization_insights(product_id)
            supply_chain_insights = await supply_chain_analyzer.get_product_supply_insights(product_id)

            return {
                "product_id": product_id,
                "demand_insights": demand_insights,
                "optimization_insights": optimization_insights,
                "supply_chain_insights": supply_chain_insights,
                "generated_at": asyncio.get_event_loop().time()
            }

        except Exception as e:
            logger.error("Failed to get inventory insights", error=str(e), product_id=product_id)
            raise HTTPException(status_code=500, detail="Failed to get inventory insights")

    @app.get("/api/v1/stock-alerts")
    async def get_stock_alerts(
        alert_type: str = None,
        severity: str = None,
        limit: int = 50
    ):
        """Get current stock alerts"""
        try:
            reorder_manager: ReorderManagerService = app.state.reorder_manager

            alerts = await reorder_manager.get_stock_alerts(
                alert_type=alert_type,
                severity=severity,
                limit=limit
            )

            return {"alerts": alerts}

        except Exception as e:
            logger.error("Failed to get stock alerts", error=str(e))
            raise HTTPException(status_code=500, detail="Failed to get stock alerts")

    @app.post("/api/v1/trigger-stock-alert")
    async def trigger_stock_alert(
        request: InventoryAlertRequest,
        background_tasks: BackgroundTasks
    ):
        """Manually trigger a stock alert"""
        try:
            reorder_manager: ReorderManagerService = app.state.reorder_manager

            alert = await reorder_manager.create_stock_alert(
                product_id=request.product_id,
                alert_type=request.alert_type,
                severity=request.severity,
                message=request.message,
                current_stock=request.current_stock,
                threshold=request.threshold
            )

            STOCK_ALERTS.labels(alert_type=request.alert_type).inc()

            # Send notifications
            background_tasks.add_task(
                send_stock_alert_notifications,
                alert
            )

            return {"alert": alert}

        except Exception as e:
            logger.error("Failed to trigger stock alert", error=str(e))
            raise HTTPException(status_code=500, detail="Failed to trigger stock alert")

    @app.get("/api/v1/inventory-dashboard")
    async def get_inventory_dashboard():
        """Get inventory dashboard data"""
        try:
            demand_forecaster: DemandForecastingService = app.state.demand_forecaster
            inventory_optimizer: InventoryOptimizationService = app.state.inventory_optimizer
            reorder_manager: ReorderManagerService = app.state.reorder_manager

            # Get dashboard metrics
            demand_metrics = await demand_forecaster.get_dashboard_metrics()
            optimization_metrics = await inventory_optimizer.get_dashboard_metrics()
            alert_metrics = await reorder_manager.get_alert_metrics()

            return {
                "demand_metrics": demand_metrics,
                "optimization_metrics": optimization_metrics,
                "alert_metrics": alert_metrics,
                "last_updated": asyncio.get_event_loop().time()
            }

        except Exception as e:
            logger.error("Failed to get inventory dashboard", error=str(e))
            raise HTTPException(status_code=500, detail="Failed to get inventory dashboard")

    @app.get("/api/v1/performance-metrics")
    async def get_performance_metrics():
        """Get inventory analytics performance metrics"""
        try:
            demand_forecaster: DemandForecastingService = app.state.demand_forecaster

            metrics = await demand_forecaster.get_performance_metrics()

            return metrics

        except Exception as e:
            logger.error("Failed to get performance metrics", error=str(e))
            raise HTTPException(status_code=500, detail="Failed to get performance metrics")

    # Metrics endpoint
    @app.get("/metrics")
    async def get_metrics():
        """Prometheus metrics endpoint"""
        return generate_latest()

    return app


async def track_forecast_accuracy(product_ids: List[str], forecast: DemandForecastResponse):
    """Track forecast accuracy for model improvement"""
    try:
        logger.info(
            "Tracking forecast accuracy",
            product_count=len(product_ids),
            forecast_horizon=forecast.forecast_horizon
        )

        # In a real implementation, this would:
        # 1. Store forecast for later comparison
        # 2. Schedule accuracy evaluation
        # 3. Update model performance metrics

    except Exception as e:
        logger.error("Failed to track forecast accuracy", error=str(e))


async def apply_optimization_recommendations(recommendations: List[Dict[str, Any]]):
    """Apply inventory optimization recommendations"""
    try:
        logger.info(
            "Applying optimization recommendations",
            recommendation_count=len(recommendations)
        )

        # In a real implementation, this would:
        # 1. Update inventory parameters
        # 2. Adjust reorder points
        # 3. Modify safety stock levels
        # 4. Update purchase schedules

    except Exception as e:
        logger.error("Failed to apply optimization recommendations", error=str(e))


async def create_purchase_orders(recommendations: List[Dict[str, Any]]):
    """Create purchase orders based on reorder recommendations"""
    try:
        logger.info(
            "Creating purchase orders",
            recommendation_count=len(recommendations)
        )

        # In a real implementation, this would:
        # 1. Generate purchase orders
        # 2. Submit to suppliers
        # 3. Track order status
        # 4. Update expected receipt dates

    except Exception as e:
        logger.error("Failed to create purchase orders", error=str(e))


async def send_stock_alert_notifications(alert: Dict[str, Any]):
    """Send stock alert notifications"""
    try:
        logger.info(
            "Sending stock alert notifications",
            product_id=alert.get("product_id"),
            alert_type=alert.get("alert_type")
        )

        # In a real implementation, this would:
        # 1. Send to notification service
        # 2. Email/SMS relevant stakeholders
        # 3. Update dashboard alerts
        # 4. Trigger automated responses

    except Exception as e:
        logger.error("Failed to send stock alert notifications", error=str(e))


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