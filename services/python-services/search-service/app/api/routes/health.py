"""
Health check routes for Search Service
"""

from fastapi import APIRouter, HTTPException
from datetime import datetime
import structlog

from app.core import database, redis_client, service_bus

router = APIRouter()
logger = structlog.get_logger(__name__)


@router.get("/")
async def health_check():
    """Basic health check endpoint"""
    return {
        "status": "healthy",
        "service": "search-service",
        "timestamp": datetime.utcnow().isoformat(),
        "version": "1.0.0"
    }


@router.get("/detailed")
async def detailed_health_check():
    """Detailed health check with dependency status"""
    try:
        health_status = {
            "status": "healthy",
            "service": "search-service",
            "timestamp": datetime.utcnow().isoformat(),
            "version": "1.0.0",
            "dependencies": {}
        }

        # Check database
        try:
            db_healthy = await database.health_check()
            health_status["dependencies"]["database"] = {
                "status": "healthy" if db_healthy else "unhealthy",
                "checked_at": datetime.utcnow().isoformat()
            }
        except Exception as e:
            health_status["dependencies"]["database"] = {
                "status": "unhealthy",
                "error": str(e),
                "checked_at": datetime.utcnow().isoformat()
            }

        # Check Redis
        try:
            redis_healthy = await redis_client.health_check()
            health_status["dependencies"]["redis"] = {
                "status": "healthy" if redis_healthy else "unhealthy",
                "checked_at": datetime.utcnow().isoformat()
            }
        except Exception as e:
            health_status["dependencies"]["redis"] = {
                "status": "unhealthy",
                "error": str(e),
                "checked_at": datetime.utcnow().isoformat()
            }

        # Check Service Bus
        try:
            sb_healthy = await service_bus.health_check()
            health_status["dependencies"]["service_bus"] = {
                "status": "healthy" if sb_healthy else "unhealthy",
                "checked_at": datetime.utcnow().isoformat()
            }
        except Exception as e:
            health_status["dependencies"]["service_bus"] = {
                "status": "unhealthy",
                "error": str(e),
                "checked_at": datetime.utcnow().isoformat()
            }

        # Determine overall status
        unhealthy_deps = [
            dep for dep in health_status["dependencies"].values()
            if dep["status"] == "unhealthy"
        ]

        if unhealthy_deps:
            health_status["status"] = "degraded"

        return health_status

    except Exception as e:
        logger.error("Health check failed", error=str(e))
        raise HTTPException(status_code=500, detail="Health check failed")


@router.get("/ready")
async def readiness_check():
    """Readiness check for Kubernetes"""
    try:
        # Check if all critical dependencies are available
        db_healthy = await database.health_check()
        redis_healthy = await redis_client.health_check()

        if db_healthy and redis_healthy:
            return {"status": "ready"}
        else:
            raise HTTPException(status_code=503, detail="Service not ready")

    except Exception as e:
        logger.error("Readiness check failed", error=str(e))
        raise HTTPException(status_code=503, detail="Service not ready")


@router.get("/live")
async def liveness_check():
    """Liveness check for Kubernetes"""
    return {"status": "alive"}