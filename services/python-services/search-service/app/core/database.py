"""
Database connection and management for Search Service
"""

import asyncio
from typing import Optional
from sqlalchemy.ext.asyncio import AsyncSession, create_async_engine, async_sessionmaker
from sqlalchemy.orm import DeclarativeBase
import structlog

from app.core.config import get_settings

settings = get_settings()
logger = structlog.get_logger(__name__)

# Global variables for database
engine: Optional[create_async_engine] = None
async_session_maker: Optional[async_sessionmaker] = None


class Base(DeclarativeBase):
    """Base class for SQLAlchemy models"""
    pass


async def init_db() -> None:
    """Initialize database connection"""
    global engine, async_session_maker

    if not settings.DATABASE_URL:
        logger.warning("No database URL configured, skipping database initialization")
        return

    try:
        engine = create_async_engine(
            settings.DATABASE_URL,
            echo=settings.DEBUG,
            pool_size=5,
            max_overflow=10,
            pool_timeout=30,
            pool_recycle=1800,
        )

        async_session_maker = async_sessionmaker(
            engine,
            class_=AsyncSession,
            expire_on_commit=False,
        )

        # Test connection
        async with engine.begin() as conn:
            await conn.run_sync(Base.metadata.create_all)

        logger.info("Database initialized successfully")

    except Exception as e:
        logger.error("Failed to initialize database", error=str(e))
        raise


async def close_db() -> None:
    """Close database connections"""
    global engine

    if engine:
        await engine.dispose()
        logger.info("Database connections closed")


async def get_db() -> AsyncSession:
    """Get database session"""
    if not async_session_maker:
        raise RuntimeError("Database not initialized")

    async with async_session_maker() as session:
        try:
            yield session
        except Exception:
            await session.rollback()
            raise
        finally:
            await session.close()


async def health_check() -> bool:
    """Check database health"""
    if not engine:
        return False

    try:
        async with engine.begin() as conn:
            await conn.execute("SELECT 1")
        return True
    except Exception as e:
        logger.error("Database health check failed", error=str(e))
        return False