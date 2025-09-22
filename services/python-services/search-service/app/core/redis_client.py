"""
Redis client configuration for Search Service
"""

import asyncio
import json
from typing import Any, Optional, Union
import redis.asyncio as redis
import structlog

from app.core.config import get_settings

settings = get_settings()
logger = structlog.get_logger(__name__)

# Global Redis client
redis_client: Optional[redis.Redis] = None


async def init_redis() -> None:
    """Initialize Redis connection"""
    global redis_client

    try:
        redis_client = redis.Redis(
            host=settings.REDIS_HOST,
            port=settings.REDIS_PORT,
            password=settings.REDIS_PASSWORD,
            db=settings.REDIS_DB,
            decode_responses=True,
            socket_timeout=30,
            socket_connect_timeout=30,
            health_check_interval=30,
        )

        # Test connection
        await redis_client.ping()
        logger.info("Redis initialized successfully")

    except Exception as e:
        logger.error("Failed to initialize Redis", error=str(e))
        raise


async def close_redis() -> None:
    """Close Redis connection"""
    global redis_client

    if redis_client:
        await redis_client.close()
        logger.info("Redis connection closed")


def get_redis() -> redis.Redis:
    """Get Redis client"""
    if not redis_client:
        raise RuntimeError("Redis not initialized")
    return redis_client


async def set_cache(key: str, value: Any, ttl: int = 300) -> bool:
    """Set cache value with TTL"""
    try:
        client = get_redis()
        serialized_value = json.dumps(value) if not isinstance(value, str) else value
        return await client.setex(key, ttl, serialized_value)
    except Exception as e:
        logger.error("Failed to set cache", key=key, error=str(e))
        return False


async def get_cache(key: str) -> Optional[Any]:
    """Get cache value"""
    try:
        client = get_redis()
        value = await client.get(key)
        if value is None:
            return None

        # Try to deserialize JSON
        try:
            return json.loads(value)
        except json.JSONDecodeError:
            return value
    except Exception as e:
        logger.error("Failed to get cache", key=key, error=str(e))
        return None


async def delete_cache(key: str) -> bool:
    """Delete cache key"""
    try:
        client = get_redis()
        return bool(await client.delete(key))
    except Exception as e:
        logger.error("Failed to delete cache", key=key, error=str(e))
        return False


async def delete_cache_pattern(pattern: str) -> int:
    """Delete all keys matching pattern"""
    try:
        client = get_redis()
        keys = []
        async for key in client.scan_iter(match=pattern):
            keys.append(key)

        if keys:
            return await client.delete(*keys)
        return 0
    except Exception as e:
        logger.error("Failed to delete cache pattern", pattern=pattern, error=str(e))
        return 0


async def health_check() -> bool:
    """Check Redis health"""
    try:
        client = get_redis()
        await client.ping()
        return True
    except Exception as e:
        logger.error("Redis health check failed", error=str(e))
        return False