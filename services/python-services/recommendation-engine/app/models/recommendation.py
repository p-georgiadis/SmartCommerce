from pydantic import BaseModel, Field, validator
from typing import List, Optional, Dict, Any
from datetime import datetime
from enum import Enum

class ActionType(str, Enum):
    VIEW = "view"
    CLICK = "click"
    ADD_TO_CART = "add_to_cart"
    PURCHASE = "purchase"
    LIKE = "like"
    DISLIKE = "dislike"
    SHARE = "share"
    REVIEW = "review"

class RecommendationRequest(BaseModel):
    user_id: str = Field(..., description="Unique identifier for the user")
    count: int = Field(default=10, ge=1, le=50, description="Number of recommendations to return")
    category: Optional[str] = Field(None, description="Filter recommendations by category")
    exclude_purchased: bool = Field(default=True, description="Exclude already purchased items")
    include_metadata: bool = Field(default=True, description="Include product metadata in response")
    refresh_cache: bool = Field(default=False, description="Force refresh of cached recommendations")
    context: Optional[Dict[str, Any]] = Field(default=None, description="Additional context for recommendations")

class ProductRecommendation(BaseModel):
    product_id: str = Field(..., description="Unique identifier for the product")
    score: float = Field(..., ge=0, le=1, description="Recommendation confidence score")
    reason: str = Field(..., description="Explanation for why this product was recommended")
    algorithm: str = Field(..., description="Algorithm used to generate this recommendation")
    metadata: Optional[Dict[str, Any]] = Field(default=None, description="Additional product information")

    class Config:
        json_schema_extra = {
            "example": {
                "product_id": "prod_123",
                "score": 0.87,
                "reason": "Based on your purchase history and similar users",
                "algorithm": "collaborative_filtering",
                "metadata": {
                    "name": "Wireless Headphones",
                    "category": "Electronics",
                    "price": 99.99,
                    "rating": 4.5
                }
            }
        }

class RecommendationResponse(BaseModel):
    user_id: str = Field(..., description="User ID for which recommendations were generated")
    recommendations: List[ProductRecommendation] = Field(..., description="List of recommended products")
    generated_at: datetime = Field(..., description="Timestamp when recommendations were generated")
    algorithm_version: str = Field(..., description="Version of the recommendation algorithm used")
    cache_ttl: int = Field(..., description="Cache time-to-live in seconds")
    total_count: Optional[int] = Field(None, description="Total number of possible recommendations")
    metadata: Optional[Dict[str, Any]] = Field(default=None, description="Additional response metadata")

    class Config:
        json_schema_extra = {
            "example": {
                "user_id": "user_456",
                "recommendations": [
                    {
                        "product_id": "prod_123",
                        "score": 0.87,
                        "reason": "Based on your purchase history",
                        "algorithm": "collaborative_filtering"
                    }
                ],
                "generated_at": "2024-01-01T12:00:00Z",
                "algorithm_version": "hybrid_v1.0",
                "cache_ttl": 300
            }
        }

class FeedbackRequest(BaseModel):
    user_id: str = Field(..., description="Unique identifier for the user")
    product_id: str = Field(..., description="Unique identifier for the product")
    action: ActionType = Field(..., description="Type of user interaction")
    rating: Optional[float] = Field(None, ge=1, le=5, description="Explicit rating (1-5 stars)")
    timestamp: Optional[datetime] = Field(default_factory=datetime.utcnow, description="When the interaction occurred")
    session_id: Optional[str] = Field(None, description="Session identifier for the interaction")
    context: Optional[Dict[str, Any]] = Field(default=None, description="Additional context about the interaction")

    @validator('rating')
    def validate_rating(cls, v):
        if v is not None and not (1 <= v <= 5):
            raise ValueError('Rating must be between 1 and 5')
        return v

    class Config:
        json_schema_extra = {
            "example": {
                "user_id": "user_456",
                "product_id": "prod_123",
                "action": "purchase",
                "rating": 4.5,
                "timestamp": "2024-01-01T12:00:00Z",
                "session_id": "session_789"
            }
        }

class UserProfile(BaseModel):
    user_id: str = Field(..., description="Unique identifier for the user")
    preferences: List[str] = Field(default=[], description="User's category preferences")
    purchase_history: List[str] = Field(default=[], description="Previously purchased product IDs")
    view_history: List[str] = Field(default=[], description="Recently viewed product IDs")
    demographics: Dict[str, Any] = Field(default={}, description="User demographic information")
    created_at: Optional[datetime] = Field(None, description="When the profile was created")
    updated_at: Optional[datetime] = Field(None, description="When the profile was last updated")

class SimilarProductsRequest(BaseModel):
    product_id: str = Field(..., description="Product ID to find similar products for")
    count: int = Field(default=10, ge=1, le=50, description="Number of similar products to return")
    category: Optional[str] = Field(None, description="Filter similar products by category")
    include_metadata: bool = Field(default=True, description="Include product metadata in response")

class SimilarProductsResponse(BaseModel):
    product_id: str = Field(..., description="Original product ID")
    similar_products: List[ProductRecommendation] = Field(..., description="List of similar products")
    generated_at: datetime = Field(..., description="Timestamp when similar products were generated")
    algorithm: str = Field(..., description="Algorithm used to find similar products")

class TrendingProductsRequest(BaseModel):
    category: Optional[str] = Field(None, description="Filter trending products by category")
    count: int = Field(default=20, ge=1, le=100, description="Number of trending products to return")
    time_window: str = Field(default="24h", description="Time window for trending calculation (24h, 7d, 30d)")
    include_metadata: bool = Field(default=True, description="Include product metadata in response")

class TrendingProductsResponse(BaseModel):
    category: Optional[str] = Field(None, description="Category filter applied")
    time_window: str = Field(..., description="Time window used for calculation")
    trending_products: List[ProductRecommendation] = Field(..., description="List of trending products")
    generated_at: datetime = Field(..., description="Timestamp when trending products were generated")

class ModelMetrics(BaseModel):
    model_name: str = Field(..., description="Name of the recommendation model")
    accuracy: float = Field(..., description="Model accuracy score")
    precision: float = Field(..., description="Model precision score")
    recall: float = Field(..., description="Model recall score")
    f1_score: float = Field(..., description="Model F1 score")
    training_date: datetime = Field(..., description="When the model was last trained")
    sample_size: int = Field(..., description="Number of samples used for training")

class HealthResponse(BaseModel):
    status: str = Field(..., description="Service health status")
    timestamp: datetime = Field(..., description="Health check timestamp")
    version: str = Field(..., description="Service version")
    dependencies: Dict[str, str] = Field(..., description="Status of external dependencies")
    metrics: Optional[Dict[str, Any]] = Field(default=None, description="Performance metrics")

class ErrorResponse(BaseModel):
    error: str = Field(..., description="Error type")
    message: str = Field(..., description="Error message")
    timestamp: datetime = Field(..., description="Error timestamp")
    request_id: Optional[str] = Field(None, description="Request identifier for debugging")

    class Config:
        json_schema_extra = {
            "example": {
                "error": "ValidationError",
                "message": "Invalid user_id format",
                "timestamp": "2024-01-01T12:00:00Z",
                "request_id": "req_123"
            }
        }