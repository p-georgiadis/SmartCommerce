"""Configuration settings for the Price Optimization Service"""

import os
from typing import List, Optional
from functools import lru_cache

from pydantic import Field
from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    """Application settings"""

    model_config = SettingsConfigDict(
        env_file=".env",
        env_file_encoding="utf-8",
        case_sensitive=True
    )

    # Application
    APP_NAME: str = "SmartCommerce Price Optimization Service"
    ENVIRONMENT: str = Field(default="development", description="Environment (development, staging, production)")
    DEBUG: bool = Field(default=False, description="Debug mode")
    LOG_LEVEL: str = Field(default="INFO", description="Logging level")

    # API Configuration
    API_V1_STR: str = "/api/v1"
    ALLOWED_ORIGINS: List[str] = Field(
        default=["http://localhost:3000", "http://localhost:8080"],
        description="Allowed CORS origins"
    )

    # Database
    DATABASE_URL: str = Field(
        default="postgresql://postgres:password@localhost:5432/smartcommerce_pricing",
        description="Database connection URL"
    )
    DATABASE_POOL_SIZE: int = Field(default=5, description="Database connection pool size")
    DATABASE_POOL_OVERFLOW: int = Field(default=10, description="Database connection pool overflow")

    # Redis
    REDIS_HOST: str = Field(default="localhost", description="Redis host")
    REDIS_PORT: int = Field(default=6379, description="Redis port")
    REDIS_PASSWORD: Optional[str] = Field(default=None, description="Redis password")
    REDIS_DB: int = Field(default=0, description="Redis database number")
    REDIS_URL: Optional[str] = Field(default=None, description="Redis connection URL")

    # Service Bus
    SERVICE_BUS_CONNECTION_STRING: str = Field(
        default="UseDevelopmentStorage=true",
        description="Azure Service Bus connection string"
    )
    PRICING_EVENTS_TOPIC: str = Field(default="pricing-events", description="Pricing events topic")

    # Azure Key Vault
    KEY_VAULT_URL: Optional[str] = Field(default=None, description="Azure Key Vault URL")
    AZURE_CLIENT_ID: Optional[str] = Field(default=None, description="Azure Client ID")
    AZURE_CLIENT_SECRET: Optional[str] = Field(default=None, description="Azure Client Secret")
    AZURE_TENANT_ID: Optional[str] = Field(default=None, description="Azure Tenant ID")

    # Machine Learning
    MODEL_STORAGE_PATH: str = Field(default="./models", description="Path to store ML models")
    MODEL_CACHE_SIZE: int = Field(default=100, description="Model cache size")
    RETRAIN_INTERVAL_HOURS: int = Field(default=24, description="Model retrain interval in hours")
    MIN_TRAINING_SAMPLES: int = Field(default=1000, description="Minimum samples required for training")

    # Pricing Configuration
    DEFAULT_PROFIT_MARGIN: float = Field(default=0.25, description="Default profit margin (25%)")
    MIN_PROFIT_MARGIN: float = Field(default=0.05, description="Minimum profit margin (5%)")
    MAX_PRICE_CHANGE_PERCENT: float = Field(default=0.20, description="Maximum price change percentage (20%)")
    PRICE_UPDATE_COOLDOWN_HOURS: int = Field(default=6, description="Cooldown period between price updates")

    # Market Analysis
    COMPETITOR_DATA_SOURCES: List[str] = Field(
        default=["manual", "web_scraping", "api"],
        description="Available competitor data sources"
    )
    MARKET_DATA_RETENTION_DAYS: int = Field(default=90, description="Market data retention period")
    PRICE_SENSITIVITY_THRESHOLD: float = Field(default=0.1, description="Price sensitivity threshold")

    # Demand Forecasting
    FORECAST_HORIZON_DAYS: int = Field(default=30, description="Demand forecast horizon in days")
    SEASONALITY_PERIOD: int = Field(default=7, description="Seasonality period in days")
    DEMAND_SMOOTHING_FACTOR: float = Field(default=0.3, description="Demand smoothing factor")

    # External Services
    CATALOG_SERVICE_URL: str = Field(
        default="http://localhost:5002",
        description="Catalog service URL"
    )
    ORDER_SERVICE_URL: str = Field(
        default="http://localhost:5001",
        description="Order service URL"
    )
    ANALYTICS_SERVICE_URL: str = Field(
        default="http://localhost:8004",
        description="Analytics service URL"
    )

    # Monitoring
    ENABLE_METRICS: bool = Field(default=True, description="Enable Prometheus metrics")
    METRICS_PORT: int = Field(default=9090, description="Metrics server port")
    HEALTH_CHECK_INTERVAL: int = Field(default=30, description="Health check interval in seconds")

    # Cache Configuration
    CACHE_TTL_SECONDS: int = Field(default=300, description="Default cache TTL in seconds")
    PRICE_CACHE_TTL_SECONDS: int = Field(default=600, description="Price cache TTL in seconds")
    MARKET_DATA_CACHE_TTL_SECONDS: int = Field(default=1800, description="Market data cache TTL")

    # Rate Limiting
    RATE_LIMIT_REQUESTS: int = Field(default=100, description="Rate limit requests per minute")
    RATE_LIMIT_WINDOW: int = Field(default=60, description="Rate limit window in seconds")

    # Security
    SECRET_KEY: str = Field(
        default="your-secret-key-change-in-production",
        description="Secret key for encryption"
    )
    API_KEY_HEADER: str = Field(default="X-API-Key", description="API key header name")

    @property
    def redis_dsn(self) -> str:
        """Redis connection DSN"""
        if self.REDIS_URL:
            return self.REDIS_URL

        auth = f":{self.REDIS_PASSWORD}@" if self.REDIS_PASSWORD else ""
        return f"redis://{auth}{self.REDIS_HOST}:{self.REDIS_PORT}/{self.REDIS_DB}"

    @property
    def is_development(self) -> bool:
        """Check if running in development mode"""
        return self.ENVIRONMENT.lower() == "development"

    @property
    def is_production(self) -> bool:
        """Check if running in production mode"""
        return self.ENVIRONMENT.lower() == "production"

    class Config:
        """Pydantic configuration"""
        case_sensitive = True
        env_file = ".env"


@lru_cache()
def get_settings() -> Settings:
    """Get cached application settings"""
    return Settings()