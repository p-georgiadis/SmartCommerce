"""Pydantic models for the Price Optimization Service"""

from datetime import datetime, date
from typing import Dict, List, Optional, Any
from enum import Enum

from pydantic import BaseModel, Field, validator


class PricingStrategy(str, Enum):
    """Pricing strategy options"""
    COMPETITIVE = "competitive"
    PREMIUM = "premium"
    PENETRATION = "penetration"
    SKIMMING = "skimming"
    VALUE_BASED = "value_based"
    COST_PLUS = "cost_plus"


class MarketCondition(str, Enum):
    """Market condition types"""
    STABLE = "stable"
    GROWING = "growing"
    DECLINING = "declining"
    VOLATILE = "volatile"
    SEASONAL_HIGH = "seasonal_high"
    SEASONAL_LOW = "seasonal_low"


class PriceChangeReason(str, Enum):
    """Reasons for price changes"""
    COMPETITIVE_PRESSURE = "competitive_pressure"
    DEMAND_INCREASE = "demand_increase"
    DEMAND_DECREASE = "demand_decrease"
    COST_CHANGE = "cost_change"
    INVENTORY_LEVEL = "inventory_level"
    SEASONAL_ADJUSTMENT = "seasonal_adjustment"
    PROMOTION = "promotion"
    MARKET_POSITION = "market_position"


# Request Models
class PricingConstraints(BaseModel):
    """Pricing constraints for optimization"""
    min_price: Optional[float] = Field(None, description="Minimum allowed price")
    max_price: Optional[float] = Field(None, description="Maximum allowed price")
    min_margin: Optional[float] = Field(None, description="Minimum profit margin")
    max_discount: Optional[float] = Field(None, description="Maximum discount percentage")
    round_to_nearest: Optional[float] = Field(None, description="Round price to nearest value")
    psychological_pricing: bool = Field(default=False, description="Apply psychological pricing (.99)")
    competitor_price_buffer: Optional[float] = Field(None, description="Buffer from competitor prices")


class DemandData(BaseModel):
    """Historical demand data"""
    daily_sales: List[int] = Field(description="Daily sales quantities")
    dates: List[date] = Field(description="Corresponding dates")
    price_points: List[float] = Field(description="Historical prices")
    promotional_periods: Optional[List[tuple]] = Field(None, description="Promotional date ranges")


class CompetitorPrice(BaseModel):
    """Competitor pricing information"""
    competitor_id: str = Field(description="Competitor identifier")
    competitor_name: str = Field(description="Competitor name")
    price: float = Field(description="Competitor price")
    last_updated: datetime = Field(description="Last price update timestamp")
    product_similarity_score: Optional[float] = Field(None, description="Product similarity score 0-1")
    market_share: Optional[float] = Field(None, description="Competitor market share")


class PriceOptimizationRequest(BaseModel):
    """Price optimization request"""
    product_id: str = Field(description="Product identifier")
    current_price: float = Field(description="Current product price")
    cost: float = Field(description="Product cost")
    inventory_level: int = Field(description="Current inventory level")
    category: Optional[str] = Field(None, description="Product category")

    competitor_prices: List[CompetitorPrice] = Field(description="Competitor pricing data")
    demand_data: Optional[DemandData] = Field(None, description="Historical demand data")
    constraints: Optional[PricingConstraints] = Field(None, description="Pricing constraints")

    strategy: PricingStrategy = Field(default=PricingStrategy.VALUE_BASED, description="Pricing strategy")
    target_margin: Optional[float] = Field(None, description="Target profit margin")
    market_conditions: Optional[MarketCondition] = Field(None, description="Current market conditions")

    auto_apply: bool = Field(default=False, description="Automatically apply recommended price")

    @validator('current_price', 'cost')
    def validate_positive_values(cls, v):
        if v <= 0:
            raise ValueError('Price and cost must be positive')
        return v

    @validator('cost')
    def validate_cost_vs_price(cls, v, values):
        if 'current_price' in values and v >= values['current_price']:
            raise ValueError('Cost cannot be greater than or equal to current price')
        return v


class ProductInfo(BaseModel):
    """Product information for bulk optimization"""
    product_id: str
    current_price: float
    cost: float
    inventory_level: int
    category: Optional[str] = None
    demand_data: Optional[DemandData] = None
    constraints: Optional[PricingConstraints] = None


class BulkPriceOptimizationRequest(BaseModel):
    """Bulk price optimization request"""
    products: List[ProductInfo] = Field(description="List of products to optimize")
    market_conditions: Optional[MarketCondition] = Field(None, description="Global market conditions")
    global_constraints: Optional[PricingConstraints] = Field(None, description="Global pricing constraints")
    strategy: PricingStrategy = Field(default=PricingStrategy.VALUE_BASED, description="Global pricing strategy")
    auto_apply: bool = Field(default=False, description="Automatically apply recommended prices")


class MarketAnalysisRequest(BaseModel):
    """Market analysis request"""
    category: str = Field(description="Product category to analyze")
    timeframe: int = Field(default=30, description="Analysis timeframe in days")
    competitors: Optional[List[str]] = Field(None, description="Specific competitors to include")
    include_seasonality: bool = Field(default=True, description="Include seasonality analysis")
    include_trends: bool = Field(default=True, description="Include trend analysis")


# Response Models
class PriceElasticity(BaseModel):
    """Price elasticity analysis"""
    elasticity_coefficient: float = Field(description="Price elasticity coefficient")
    demand_sensitivity: str = Field(description="Demand sensitivity level")
    optimal_price_range: tuple = Field(description="Optimal price range")
    confidence_score: float = Field(description="Confidence score 0-1")


class RevenueProjection(BaseModel):
    """Revenue projection for different price points"""
    price_point: float = Field(description="Price point")
    projected_demand: int = Field(description="Projected demand")
    projected_revenue: float = Field(description="Projected revenue")
    projected_profit: float = Field(description="Projected profit")
    confidence_interval: tuple = Field(description="95% confidence interval")


class PriceOptimizationResponse(BaseModel):
    """Price optimization response"""
    product_id: str = Field(description="Product identifier")
    current_price: float = Field(description="Current price")
    recommended_price: float = Field(description="Recommended optimal price")
    price_change: float = Field(description="Absolute price change")
    price_change_percent: float = Field(description="Percentage price change")

    expected_demand_change: float = Field(description="Expected demand change percentage")
    expected_revenue_change: float = Field(description="Expected revenue change percentage")
    expected_profit_change: float = Field(description="Expected profit change percentage")

    confidence_score: float = Field(description="Optimization confidence score 0-1")
    primary_reason: PriceChangeReason = Field(description="Primary reason for price change")
    secondary_reasons: List[PriceChangeReason] = Field(description="Secondary reasons")

    price_elasticity: Optional[PriceElasticity] = Field(None, description="Price elasticity analysis")
    revenue_projections: List[RevenueProjection] = Field(description="Revenue projections")

    competitor_analysis: Dict[str, Any] = Field(description="Competitor analysis results")
    market_position: str = Field(description="Recommended market position")

    risk_factors: List[str] = Field(description="Identified risk factors")
    optimization_timestamp: datetime = Field(description="Optimization timestamp")

    # Implementation details
    model_version: str = Field(description="ML model version used")
    data_quality_score: float = Field(description="Input data quality score 0-1")

    @validator('confidence_score', 'data_quality_score')
    def validate_score_range(cls, v):
        if not 0 <= v <= 1:
            raise ValueError('Score must be between 0 and 1')
        return v


class MarketAnalysisResponse(BaseModel):
    """Market analysis response"""
    category: str = Field(description="Analyzed category")
    analysis_period: tuple = Field(description="Analysis period (start, end)")

    market_size: float = Field(description="Estimated market size")
    market_growth_rate: float = Field(description="Market growth rate")
    market_condition: MarketCondition = Field(description="Current market condition")

    average_price: float = Field(description="Category average price")
    price_range: tuple = Field(description="Price range (min, max)")
    price_volatility: float = Field(description="Price volatility index")

    top_competitors: List[Dict[str, Any]] = Field(description="Top competitors analysis")
    market_share_distribution: Dict[str, float] = Field(description="Market share by competitor")

    pricing_trends: Dict[str, Any] = Field(description="Pricing trends analysis")
    seasonality_patterns: Optional[Dict[str, Any]] = Field(None, description="Seasonality patterns")

    demand_drivers: List[str] = Field(description="Key demand drivers")
    risk_factors: List[str] = Field(description="Market risk factors")
    opportunities: List[str] = Field(description="Market opportunities")

    recommendations: List[str] = Field(description="Strategic recommendations")
    confidence_score: float = Field(description="Analysis confidence score 0-1")


class PricingInsights(BaseModel):
    """Comprehensive pricing insights"""
    product_id: str = Field(description="Product identifier")

    current_metrics: Dict[str, Any] = Field(description="Current performance metrics")
    historical_performance: Dict[str, Any] = Field(description="Historical performance data")

    price_sensitivity_analysis: PriceElasticity = Field(description="Price sensitivity analysis")
    competitive_position: Dict[str, Any] = Field(description="Competitive positioning")
    demand_forecast: Dict[str, Any] = Field(description="Demand forecast")

    optimization_opportunities: List[Dict[str, Any]] = Field(description="Optimization opportunities")
    risk_assessment: Dict[str, Any] = Field(description="Risk assessment")

    strategic_recommendations: List[str] = Field(description="Strategic recommendations")
    tactical_actions: List[str] = Field(description="Tactical actions")

    generated_at: datetime = Field(description="Insights generation timestamp")


# Event Models
class PriceChangeEvent(BaseModel):
    """Price change event"""
    event_id: str = Field(description="Event identifier")
    product_id: str = Field(description="Product identifier")
    old_price: float = Field(description="Previous price")
    new_price: float = Field(description="New price")
    change_reason: PriceChangeReason = Field(description="Reason for change")
    applied_by: str = Field(description="Who applied the change")
    applied_at: datetime = Field(description="When change was applied")
    optimization_id: Optional[str] = Field(None, description="Related optimization ID")


class ModelTrainingEvent(BaseModel):
    """Model training event"""
    event_id: str = Field(description="Event identifier")
    model_type: str = Field(description="Type of model trained")
    training_data_size: int = Field(description="Size of training dataset")
    model_accuracy: float = Field(description="Model accuracy score")
    training_duration: float = Field(description="Training duration in seconds")
    model_version: str = Field(description="New model version")
    trained_at: datetime = Field(description="Training completion timestamp")