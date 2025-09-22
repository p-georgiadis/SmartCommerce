"""
SmartCommerce Fraud Detection Service

This service provides real-time fraud detection using advanced machine learning
algorithms to identify suspicious transactions, user behaviors, and potential
security threats in the e-commerce platform.
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
from app.api.routes import fraud, analytics, health
from app.services.fraud_detector import FraudDetectionService
from app.services.risk_analyzer import RiskAnalyzerService
from app.services.pattern_detector import PatternDetectorService
from app.services.real_time_monitor import RealTimeMonitorService
from app.models.schemas import (
    TransactionAnalysisRequest,
    TransactionAnalysisResponse,
    UserBehaviorAnalysisRequest,
    BulkTransactionAnalysisRequest,
    FraudAlert,
    RiskAssessmentRequest
)

# Metrics
TRANSACTION_CHECKS = Counter('fraud_transactions_checked_total', 'Total transactions checked for fraud')
FRAUD_DETECTED = Counter('fraud_detected_total', 'Total fraud cases detected', ['fraud_type'])
PROCESSING_TIME = Histogram('fraud_detection_processing_seconds', 'Fraud detection processing time')
FALSE_POSITIVES = Counter('fraud_false_positives_total', 'False positive fraud detections')

settings = get_settings()
logger = structlog.get_logger()


@asynccontextmanager
async def lifespan(app: FastAPI):
    """Application lifespan manager"""
    logger.info("Starting Fraud Detection Service")

    # Initialize services
    await init_db()
    await init_redis()
    await init_service_bus()

    # Initialize fraud detection services
    fraud_detector = FraudDetectionService()
    risk_analyzer = RiskAnalyzerService()
    pattern_detector = PatternDetectorService()
    real_time_monitor = RealTimeMonitorService()

    # Load ML models and rules
    await fraud_detector.load_models()
    await risk_analyzer.load_models()
    await pattern_detector.load_patterns()
    await real_time_monitor.start_monitoring()

    # Store services in app state
    app.state.fraud_detector = fraud_detector
    app.state.risk_analyzer = risk_analyzer
    app.state.pattern_detector = pattern_detector
    app.state.real_time_monitor = real_time_monitor

    logger.info("Fraud Detection Service started successfully")

    yield

    # Cleanup
    logger.info("Shutting down Fraud Detection Service")
    await real_time_monitor.stop_monitoring()
    await close_service_bus()
    await close_redis()
    await close_db()
    logger.info("Fraud Detection Service stopped")


def create_app() -> FastAPI:
    """Create and configure FastAPI application"""

    # Setup logging
    setup_logging()

    app = FastAPI(
        title="SmartCommerce Fraud Detection Service",
        description="Real-time fraud detection and risk analysis using machine learning",
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
    app.include_router(fraud.router, prefix="/api/v1/fraud", tags=["fraud"])
    app.include_router(analytics.router, prefix="/api/v1/analytics", tags=["analytics"])

    # Core fraud detection endpoints
    @app.post("/api/v1/analyze-transaction", response_model=TransactionAnalysisResponse)
    async def analyze_transaction(
        request: TransactionAnalysisRequest,
        background_tasks: BackgroundTasks
    ):
        """Analyze a transaction for fraud indicators"""
        try:
            TRANSACTION_CHECKS.inc()

            fraud_detector: FraudDetectionService = app.state.fraud_detector
            risk_analyzer: RiskAnalyzerService = app.state.risk_analyzer

            # Perform fraud analysis
            fraud_result = await fraud_detector.analyze_transaction(
                transaction_data=request.transaction,
                user_data=request.user,
                device_data=request.device,
                context_data=request.context
            )

            # Perform risk assessment
            risk_result = await risk_analyzer.assess_transaction_risk(
                transaction=request.transaction,
                fraud_indicators=fraud_result.indicators
            )

            # Combine results
            response = TransactionAnalysisResponse(
                transaction_id=request.transaction.transaction_id,
                fraud_score=fraud_result.fraud_score,
                risk_level=risk_result.risk_level,
                decision=fraud_result.decision,
                indicators=fraud_result.indicators,
                risk_factors=risk_result.risk_factors,
                recommendations=fraud_result.recommendations,
                confidence_score=fraud_result.confidence_score,
                processing_time_ms=fraud_result.processing_time_ms,
                model_version=fraud_result.model_version,
                analysis_timestamp=fraud_result.analysis_timestamp
            )

            # Record fraud detection if high risk
            if fraud_result.fraud_score > 0.7:
                FRAUD_DETECTED.labels(fraud_type=fraud_result.primary_indicator or "unknown").inc()

                # Schedule immediate alert for high-risk transactions
                background_tasks.add_task(
                    send_fraud_alert,
                    request.transaction.transaction_id,
                    fraud_result.fraud_score,
                    fraud_result.primary_indicator
                )

            return response

        except Exception as e:
            logger.error("Transaction analysis failed", error=str(e),
                        transaction_id=request.transaction.transaction_id)
            raise HTTPException(status_code=500, detail="Transaction analysis failed")

    @app.post("/api/v1/bulk-analyze", response_model=Dict[str, TransactionAnalysisResponse])
    async def bulk_analyze_transactions(
        request: BulkTransactionAnalysisRequest,
        background_tasks: BackgroundTasks
    ):
        """Analyze multiple transactions for fraud"""
        try:
            fraud_detector: FraudDetectionService = app.state.fraud_detector

            results = await fraud_detector.bulk_analyze_transactions(
                transactions=request.transactions,
                analysis_options=request.options
            )

            # Handle high-risk transactions
            high_risk_transactions = [
                (tid, result) for tid, result in results.items()
                if result.fraud_score > 0.7
            ]

            if high_risk_transactions:
                background_tasks.add_task(
                    handle_high_risk_transactions,
                    high_risk_transactions
                )

            return results

        except Exception as e:
            logger.error("Bulk transaction analysis failed", error=str(e))
            raise HTTPException(status_code=500, detail="Bulk analysis failed")

    @app.post("/api/v1/analyze-user-behavior")
    async def analyze_user_behavior(request: UserBehaviorAnalysisRequest):
        """Analyze user behavior patterns for anomalies"""
        try:
            pattern_detector: PatternDetectorService = app.state.pattern_detector

            analysis = await pattern_detector.analyze_user_behavior(
                user_id=request.user_id,
                behavior_data=request.behavior_data,
                timeframe=request.timeframe
            )

            return analysis

        except Exception as e:
            logger.error("User behavior analysis failed", error=str(e), user_id=request.user_id)
            raise HTTPException(status_code=500, detail="User behavior analysis failed")

    @app.post("/api/v1/assess-risk")
    async def assess_risk(request: RiskAssessmentRequest):
        """Perform comprehensive risk assessment"""
        try:
            risk_analyzer: RiskAnalyzerService = app.state.risk_analyzer

            assessment = await risk_analyzer.comprehensive_risk_assessment(
                entity_type=request.entity_type,
                entity_data=request.entity_data,
                assessment_scope=request.scope
            )

            return assessment

        except Exception as e:
            logger.error("Risk assessment failed", error=str(e))
            raise HTTPException(status_code=500, detail="Risk assessment failed")

    @app.get("/api/v1/fraud-alerts")
    async def get_recent_fraud_alerts(
        limit: int = 50,
        severity: str = None
    ):
        """Get recent fraud alerts"""
        try:
            fraud_detector: FraudDetectionService = app.state.fraud_detector

            alerts = await fraud_detector.get_recent_alerts(
                limit=limit,
                severity_filter=severity
            )

            return {"alerts": alerts}

        except Exception as e:
            logger.error("Failed to get fraud alerts", error=str(e))
            raise HTTPException(status_code=500, detail="Failed to get fraud alerts")

    @app.get("/api/v1/fraud-patterns")
    async def get_fraud_patterns():
        """Get detected fraud patterns and trends"""
        try:
            pattern_detector: PatternDetectorService = app.state.pattern_detector

            patterns = await pattern_detector.get_current_fraud_patterns()

            return {"patterns": patterns}

        except Exception as e:
            logger.error("Failed to get fraud patterns", error=str(e))
            raise HTTPException(status_code=500, detail="Failed to get fraud patterns")

    @app.post("/api/v1/report-false-positive")
    async def report_false_positive(
        transaction_id: str,
        user_feedback: str = None
    ):
        """Report a false positive fraud detection"""
        try:
            fraud_detector: FraudDetectionService = app.state.fraud_detector

            await fraud_detector.handle_false_positive(
                transaction_id=transaction_id,
                feedback=user_feedback
            )

            FALSE_POSITIVES.inc()

            return {"message": "False positive reported successfully"}

        except Exception as e:
            logger.error("Failed to report false positive", error=str(e),
                        transaction_id=transaction_id)
            raise HTTPException(status_code=500, detail="Failed to report false positive")

    @app.get("/api/v1/fraud-stats")
    async def get_fraud_statistics(
        timeframe: str = "24h"
    ):
        """Get fraud detection statistics"""
        try:
            fraud_detector: FraudDetectionService = app.state.fraud_detector

            stats = await fraud_detector.get_fraud_statistics(timeframe=timeframe)

            return stats

        except Exception as e:
            logger.error("Failed to get fraud statistics", error=str(e))
            raise HTTPException(status_code=500, detail="Failed to get fraud statistics")

    # Real-time monitoring endpoints
    @app.get("/api/v1/monitoring/status")
    async def get_monitoring_status():
        """Get real-time monitoring status"""
        try:
            monitor: RealTimeMonitorService = app.state.real_time_monitor

            status = await monitor.get_monitoring_status()

            return status

        except Exception as e:
            logger.error("Failed to get monitoring status", error=str(e))
            raise HTTPException(status_code=500, detail="Failed to get monitoring status")

    # Metrics endpoint
    @app.get("/metrics")
    async def get_metrics():
        """Prometheus metrics endpoint"""
        return generate_latest()

    return app


async def send_fraud_alert(transaction_id: str, fraud_score: float, fraud_type: str):
    """Send fraud alert for high-risk transactions"""
    try:
        alert = FraudAlert(
            transaction_id=transaction_id,
            fraud_score=fraud_score,
            fraud_type=fraud_type,
            severity="high" if fraud_score > 0.9 else "medium",
            timestamp=asyncio.get_event_loop().time()
        )

        # In a real implementation, this would:
        # 1. Send to notification service
        # 2. Log to fraud database
        # 3. Trigger automated responses
        # 4. Alert fraud investigation team

        logger.warning(
            "Fraud alert generated",
            transaction_id=transaction_id,
            fraud_score=fraud_score,
            fraud_type=fraud_type
        )

    except Exception as e:
        logger.error(
            "Failed to send fraud alert",
            error=str(e),
            transaction_id=transaction_id
        )


async def handle_high_risk_transactions(transactions: List[tuple]):
    """Handle multiple high-risk transactions"""
    try:
        for transaction_id, result in transactions:
            await send_fraud_alert(
                transaction_id,
                result.fraud_score,
                result.primary_indicator or "unknown"
            )

        logger.info(
            "Processed high-risk transactions",
            count=len(transactions)
        )

    except Exception as e:
        logger.error(
            "Failed to handle high-risk transactions",
            error=str(e),
            count=len(transactions)
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