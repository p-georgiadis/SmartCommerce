"""
Configuration settings for the Search Service
"""

import os
from typing import List, Optional
from pydantic import BaseSettings


class Settings(BaseSettings):
    """Application settings"""

    # Application
    APP_NAME: str = "SmartCommerce Search Service"
    APP_VERSION: str = "1.0.0"
    ENVIRONMENT: str = os.getenv("ENVIRONMENT", "development")
    DEBUG: bool = os.getenv("DEBUG", "false").lower() == "true"

    # API Configuration
    API_PREFIX: str = "/api/v1"
    ALLOWED_ORIGINS: List[str] = [
        "http://localhost:3000",
        "https://*.azurewebsites.net",
        "https://*.azurecontainerapps.io"
    ]

    # Database
    DATABASE_URL: Optional[str] = os.getenv("DATABASE_URL")

    # Redis Cache
    REDIS_HOST: str = os.getenv("REDIS_HOST", "localhost")
    REDIS_PORT: int = int(os.getenv("REDIS_PORT", "6379"))
    REDIS_PASSWORD: Optional[str] = os.getenv("REDIS_PASSWORD")
    REDIS_DB: int = int(os.getenv("REDIS_DB", "0"))

    # Azure Services
    AZURE_KEY_VAULT_URL: Optional[str] = os.getenv("AZURE_KEY_VAULT_URL")
    AZURE_SERVICE_BUS_CONNECTION_STRING: Optional[str] = os.getenv("AZURE_SERVICE_BUS_CONNECTION_STRING")
    AZURE_STORAGE_CONNECTION_STRING: Optional[str] = os.getenv("AZURE_STORAGE_CONNECTION_STRING")

    # Search Configuration
    ELASTICSEARCH_URL: Optional[str] = os.getenv("ELASTICSEARCH_URL")
    OPENSEARCH_URL: Optional[str] = os.getenv("OPENSEARCH_URL")
    AZURE_SEARCH_ENDPOINT: Optional[str] = os.getenv("AZURE_SEARCH_ENDPOINT")
    AZURE_SEARCH_API_KEY: Optional[str] = os.getenv("AZURE_SEARCH_API_KEY")

    # ML Models
    SPACY_MODEL: str = os.getenv("SPACY_MODEL", "en_core_web_lg")
    SENTENCE_TRANSFORMER_MODEL: str = os.getenv("SENTENCE_TRANSFORMER_MODEL", "all-MiniLM-L6-v2")
    HUGGINGFACE_MODEL_CACHE: str = os.getenv("HUGGINGFACE_MODEL_CACHE", "/tmp/models")

    # Search Parameters
    DEFAULT_SEARCH_LIMIT: int = int(os.getenv("DEFAULT_SEARCH_LIMIT", "20"))
    MAX_SEARCH_LIMIT: int = int(os.getenv("MAX_SEARCH_LIMIT", "100"))
    SEMANTIC_SIMILARITY_THRESHOLD: float = float(os.getenv("SEMANTIC_SIMILARITY_THRESHOLD", "0.7"))

    # Background Tasks
    INDEX_UPDATE_INTERVAL_SECONDS: int = int(os.getenv("INDEX_UPDATE_INTERVAL_SECONDS", "300"))
    ANALYTICS_CLEANUP_INTERVAL_HOURS: int = int(os.getenv("ANALYTICS_CLEANUP_INTERVAL_HOURS", "24"))

    # Monitoring
    ENABLE_METRICS: bool = os.getenv("ENABLE_METRICS", "true").lower() == "true"
    METRICS_PORT: int = int(os.getenv("METRICS_PORT", "9090"))

    # Logging
    LOG_LEVEL: str = os.getenv("LOG_LEVEL", "INFO")
    LOG_FORMAT: str = os.getenv("LOG_FORMAT", "json")

    class Config:
        env_file = ".env"
        case_sensitive = True


def get_settings() -> Settings:
    """Get application settings instance"""
    return Settings()