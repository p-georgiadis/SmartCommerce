"""
Pydantic schemas for Search Service
"""

from typing import List, Optional, Dict, Any, Union
from pydantic import BaseModel, Field
from datetime import datetime
from enum import Enum


class SearchType(str, Enum):
    """Search type enumeration"""
    KEYWORD = "keyword"
    SEMANTIC = "semantic"
    FUZZY = "fuzzy"
    EXACT = "exact"


class SortOption(str, Enum):
    """Sort option enumeration"""
    RELEVANCE = "relevance"
    PRICE_ASC = "price_asc"
    PRICE_DESC = "price_desc"
    NAME_ASC = "name_asc"
    NAME_DESC = "name_desc"
    RATING = "rating"
    NEWEST = "newest"
    POPULARITY = "popularity"


class ProductFilter(BaseModel):
    """Product filter model"""
    category: Optional[str] = None
    brand: Optional[str] = None
    price_min: Optional[float] = None
    price_max: Optional[float] = None
    rating_min: Optional[float] = None
    in_stock: Optional[bool] = None
    tags: Optional[List[str]] = None


class Pagination(BaseModel):
    """Pagination model"""
    page: int = Field(default=1, ge=1)
    size: int = Field(default=20, ge=1, le=100)


class SearchOptions(BaseModel):
    """Search options model"""
    enable_autocorrect: bool = True
    enable_synonyms: bool = True
    enable_stemming: bool = True
    boost_exact_match: bool = True
    include_facets: bool = True
    include_suggestions: bool = True


class SearchRequest(BaseModel):
    """Standard search request model"""
    query: str = Field(..., min_length=1, max_length=500)
    filters: Optional[ProductFilter] = None
    sort_options: Optional[List[SortOption]] = None
    pagination: Optional[Pagination] = None
    options: Optional[SearchOptions] = None
    user_id: Optional[str] = None


class SemanticSearchRequest(BaseModel):
    """Semantic search request model"""
    query: str = Field(..., min_length=1, max_length=500)
    search_type: SearchType = SearchType.SEMANTIC
    similarity_threshold: float = Field(default=0.7, ge=0.1, le=1.0)
    max_results: int = Field(default=20, ge=1, le=100)
    filters: Optional[ProductFilter] = None
    user_id: Optional[str] = None


class PersonalizedSearchRequest(BaseModel):
    """Personalized search request model"""
    query: str = Field(..., min_length=1, max_length=500)
    user_id: str = Field(..., min_length=1)
    filters: Optional[ProductFilter] = None
    sort_options: Optional[List[SortOption]] = None
    pagination: Optional[Pagination] = None
    personalization_strength: float = Field(default=0.5, ge=0.0, le=1.0)
    user_context: Optional[Dict[str, Any]] = None


class AutocompleteRequest(BaseModel):
    """Autocomplete request model"""
    query: str = Field(..., min_length=2, max_length=100)
    user_id: Optional[str] = None
    max_suggestions: int = Field(default=10, ge=1, le=20)
    include_popular: bool = True


class ProductResult(BaseModel):
    """Product search result model"""
    id: str
    name: str
    description: Optional[str] = None
    price: float
    currency: str = "USD"
    brand: Optional[str] = None
    category: Optional[str] = None
    image_url: Optional[str] = None
    rating: Optional[float] = None
    review_count: Optional[int] = None
    in_stock: bool = True
    tags: Optional[List[str]] = None
    relevance_score: Optional[float] = None
    boost_reason: Optional[str] = None


class SearchFacet(BaseModel):
    """Search facet model"""
    name: str
    values: List[Dict[str, Union[str, int]]]
    facet_type: str = "terms"  # terms, range, date_range


class SearchResponse(BaseModel):
    """Search response model"""
    query: str
    total_results: int
    products: List[ProductResult]
    facets: Optional[Dict[str, SearchFacet]] = None
    suggestions: Optional[List[str]] = None
    search_time_ms: int
    page: Optional[int] = None
    page_size: Optional[int] = None
    spell_correction: Optional[str] = None
    personalized: bool = False


class IndexOptions(BaseModel):
    """Indexing options model"""
    update_synonyms: bool = True
    update_categories: bool = True
    async_processing: bool = False
    priority: int = Field(default=5, ge=1, le=10)


class IndexRequest(BaseModel):
    """Product indexing request model"""
    product_data: Dict[str, Any]
    options: Optional[IndexOptions] = None


class SearchAnalyticsRequest(BaseModel):
    """Search analytics request model"""
    timeframe: str = "24h"
    user_id: Optional[str] = None
    query_filter: Optional[str] = None
    category_filter: Optional[str] = None


class AutocompleteSuggestion(BaseModel):
    """Autocomplete suggestion model"""
    text: str
    type: str = "query"  # query, product, category, brand
    score: float
    metadata: Optional[Dict[str, Any]] = None


class SearchSuggestion(BaseModel):
    """Search suggestion model"""
    query: str
    reason: str  # popular, trending, personalized, related
    score: float
    category: Optional[str] = None


class TrendingSearch(BaseModel):
    """Trending search model"""
    query: str
    search_count: int
    growth_rate: Optional[float] = None
    category: Optional[str] = None
    timeframe: str


class SearchAnalytics(BaseModel):
    """Search analytics model"""
    timeframe: str
    total_searches: int
    unique_users: int
    avg_search_time_ms: float
    zero_result_rate: float
    top_queries: List[Dict[str, Union[str, int]]]
    search_trends: Dict[str, Any]
    performance_metrics: Dict[str, float]


class UserSearchProfile(BaseModel):
    """User search profile model"""
    user_id: str
    search_preferences: Dict[str, Any]
    favorite_categories: List[str]
    search_history: List[str]
    interaction_patterns: Dict[str, float]
    last_updated: datetime


class SearchHealth(BaseModel):
    """Search health status model"""
    status: str  # healthy, degraded, unhealthy
    indices_status: Dict[str, Dict[str, Any]]
    performance_metrics: Dict[str, float]
    error_rate: float
    last_check: datetime


class IndexingStatus(BaseModel):
    """Indexing status model"""
    total_documents: int
    pending_operations: int
    processing_rate: float
    avg_processing_time_ms: float
    last_updated: datetime
    errors: List[Dict[str, Any]]