"""Pydantic models for the Fraud Detection Service"""

from datetime import datetime
from typing import Dict, List, Optional, Any, Union
from enum import Enum
from decimal import Decimal

from pydantic import BaseModel, Field, validator
from geopy import Point


class FraudDecision(str, Enum):
    """Fraud detection decisions"""
    APPROVE = "approve"
    DECLINE = "decline"
    REVIEW = "review"
    CHALLENGE = "challenge"


class RiskLevel(str, Enum):
    """Risk levels"""
    LOW = "low"
    MEDIUM = "medium"
    HIGH = "high"
    CRITICAL = "critical"


class FraudType(str, Enum):
    """Types of fraud"""
    CARD_NOT_PRESENT = "card_not_present"
    ACCOUNT_TAKEOVER = "account_takeover"
    SYNTHETIC_IDENTITY = "synthetic_identity"
    FRIENDLY_FRAUD = "friendly_fraud"
    MERCHANT_FRAUD = "merchant_fraud"
    REFUND_FRAUD = "refund_fraud"
    PROMO_ABUSE = "promo_abuse"
    VELOCITY_FRAUD = "velocity_fraud"
    BEHAVIORAL_ANOMALY = "behavioral_anomaly"
    DEVICE_FRAUD = "device_fraud"


class EntityType(str, Enum):
    """Entity types for risk assessment"""
    USER = "user"
    MERCHANT = "merchant"
    DEVICE = "device"
    IP_ADDRESS = "ip_address"
    PAYMENT_METHOD = "payment_method"


# Base Models
class DeviceInfo(BaseModel):
    """Device information for fraud analysis"""
    device_id: str = Field(description="Unique device identifier")
    device_type: str = Field(description="Device type (mobile, desktop, tablet)")
    os: str = Field(description="Operating system")
    browser: str = Field(description="Browser type and version")
    ip_address: str = Field(description="IP address")
    user_agent: str = Field(description="User agent string")
    screen_resolution: Optional[str] = Field(None, description="Screen resolution")
    timezone: Optional[str] = Field(None, description="Device timezone")
    language: Optional[str] = Field(None, description="Browser language")
    geolocation: Optional[Dict[str, float]] = Field(None, description="GPS coordinates")
    is_mobile: bool = Field(description="Is mobile device")
    is_proxy: Optional[bool] = Field(None, description="Is using proxy/VPN")
    device_fingerprint: Optional[str] = Field(None, description="Device fingerprint hash")


class PaymentMethod(BaseModel):
    """Payment method information"""
    payment_type: str = Field(description="Payment type (card, wallet, bank_transfer)")
    card_bin: Optional[str] = Field(None, description="Card BIN (first 6 digits)")
    card_last_four: Optional[str] = Field(None, description="Last 4 digits of card")
    card_brand: Optional[str] = Field(None, description="Card brand (visa, mastercard, etc.)")
    card_type: Optional[str] = Field(None, description="Card type (credit, debit)")
    issuing_bank: Optional[str] = Field(None, description="Issuing bank")
    issuing_country: Optional[str] = Field(None, description="Card issuing country")
    wallet_provider: Optional[str] = Field(None, description="Digital wallet provider")
    is_tokenized: bool = Field(default=False, description="Is payment tokenized")
    token_confidence: Optional[float] = Field(None, description="Token confidence score")


class TransactionData(BaseModel):
    """Transaction data for fraud analysis"""
    transaction_id: str = Field(description="Unique transaction identifier")
    user_id: str = Field(description="User identifier")
    merchant_id: Optional[str] = Field(None, description="Merchant identifier")
    amount: Decimal = Field(description="Transaction amount")
    currency: str = Field(description="Currency code")
    transaction_type: str = Field(description="Transaction type (purchase, refund, etc.)")
    payment_method: PaymentMethod = Field(description="Payment method details")

    # Geographic data
    billing_address: Optional[Dict[str, str]] = Field(None, description="Billing address")
    shipping_address: Optional[Dict[str, str]] = Field(None, description="Shipping address")

    # Transaction context
    merchant_category: Optional[str] = Field(None, description="Merchant category code")
    product_category: Optional[str] = Field(None, description="Product category")
    is_recurring: bool = Field(default=False, description="Is recurring transaction")
    is_international: bool = Field(default=False, description="Is international transaction")

    # Timing
    transaction_time: datetime = Field(description="Transaction timestamp")
    time_since_account_creation: Optional[int] = Field(None, description="Days since account creation")

    # Velocity indicators
    hourly_transaction_count: Optional[int] = Field(None, description="Transactions in last hour")
    daily_transaction_count: Optional[int] = Field(None, description="Transactions today")
    daily_amount_total: Optional[Decimal] = Field(None, description="Total amount today")

    @validator('amount')
    def validate_amount(cls, v):
        if v <= 0:
            raise ValueError('Transaction amount must be positive')
        return v


class UserData(BaseModel):
    """User data for fraud analysis"""
    user_id: str = Field(description="User identifier")
    account_age_days: int = Field(description="Account age in days")
    email_verified: bool = Field(description="Is email verified")
    phone_verified: bool = Field(description="Is phone verified")
    kyc_status: str = Field(description="KYC verification status")

    # User behavior
    login_frequency: Optional[float] = Field(None, description="Average logins per day")
    transaction_frequency: Optional[float] = Field(None, description="Average transactions per day")
    average_transaction_amount: Optional[Decimal] = Field(None, description="Average transaction amount")

    # Risk indicators
    previous_fraud_reports: int = Field(default=0, description="Number of previous fraud reports")
    chargebacks_count: int = Field(default=0, description="Number of chargebacks")
    failed_login_attempts: int = Field(default=0, description="Recent failed login attempts")

    # Profile data
    country: Optional[str] = Field(None, description="User country")
    registration_ip: Optional[str] = Field(None, description="Registration IP address")
    last_login_time: Optional[datetime] = Field(None, description="Last login timestamp")
    profile_completeness: Optional[float] = Field(None, description="Profile completeness score")


class ContextData(BaseModel):
    """Additional context for fraud analysis"""
    session_id: str = Field(description="Session identifier")
    session_duration: Optional[int] = Field(None, description="Session duration in seconds")
    pages_visited: Optional[int] = Field(None, description="Pages visited in session")
    referrer: Optional[str] = Field(None, description="Traffic referrer")

    # Timing patterns
    time_to_transaction: Optional[int] = Field(None, description="Time from login to transaction")
    typing_patterns: Optional[Dict[str, float]] = Field(None, description="Typing pattern analysis")
    mouse_movements: Optional[Dict[str, Any]] = Field(None, description="Mouse movement patterns")

    # External factors
    market_conditions: Optional[str] = Field(None, description="Current market conditions")
    holiday_indicator: bool = Field(default=False, description="Is transaction during holiday")
    promotional_period: bool = Field(default=False, description="Is promotional period active")


class BehaviorData(BaseModel):
    """User behavior data for pattern analysis"""
    login_times: List[datetime] = Field(description="Recent login timestamps")
    transaction_times: List[datetime] = Field(description="Recent transaction timestamps")
    locations: List[Dict[str, float]] = Field(description="Recent location data")
    devices_used: List[str] = Field(description="Recent device IDs")
    purchase_categories: List[str] = Field(description="Recent purchase categories")
    spending_amounts: List[Decimal] = Field(description="Recent spending amounts")
    session_durations: List[int] = Field(description="Recent session durations")


# Request Models
class TransactionAnalysisRequest(BaseModel):
    """Request for transaction fraud analysis"""
    transaction: TransactionData = Field(description="Transaction to analyze")
    user: UserData = Field(description="User data")
    device: DeviceInfo = Field(description="Device information")
    context: ContextData = Field(description="Additional context")

    # Analysis options
    include_device_analysis: bool = Field(default=True, description="Include device fingerprinting")
    include_behavioral_analysis: bool = Field(default=True, description="Include behavioral analysis")
    include_network_analysis: bool = Field(default=True, description="Include network analysis")
    real_time_scoring: bool = Field(default=True, description="Use real-time scoring models")


class BulkTransactionAnalysisRequest(BaseModel):
    """Request for bulk transaction analysis"""
    transactions: List[TransactionAnalysisRequest] = Field(description="Transactions to analyze")
    options: Optional[Dict[str, Any]] = Field(None, description="Analysis options")
    priority: str = Field(default="normal", description="Processing priority")


class UserBehaviorAnalysisRequest(BaseModel):
    """Request for user behavior analysis"""
    user_id: str = Field(description="User to analyze")
    behavior_data: BehaviorData = Field(description="User behavior data")
    timeframe: int = Field(default=30, description="Analysis timeframe in days")
    include_peer_comparison: bool = Field(default=True, description="Compare with peer behavior")


class RiskAssessmentRequest(BaseModel):
    """Request for comprehensive risk assessment"""
    entity_type: EntityType = Field(description="Type of entity to assess")
    entity_data: Dict[str, Any] = Field(description="Entity data")
    scope: List[str] = Field(description="Assessment scope areas")
    include_historical: bool = Field(default=True, description="Include historical analysis")


# Response Models
class FraudIndicator(BaseModel):
    """Individual fraud indicator"""
    indicator_type: str = Field(description="Type of indicator")
    description: str = Field(description="Indicator description")
    severity: float = Field(description="Severity score 0-1")
    confidence: float = Field(description="Confidence in indicator 0-1")
    contributing_factors: List[str] = Field(description="Contributing factors")


class RiskFactor(BaseModel):
    """Risk factor in analysis"""
    factor_type: str = Field(description="Type of risk factor")
    description: str = Field(description="Factor description")
    impact_score: float = Field(description="Impact score 0-1")
    likelihood: float = Field(description="Likelihood 0-1")
    mitigation_suggestions: List[str] = Field(description="Mitigation suggestions")


class TransactionAnalysisResponse(BaseModel):
    """Response from transaction fraud analysis"""
    transaction_id: str = Field(description="Transaction identifier")
    fraud_score: float = Field(description="Fraud score 0-1")
    risk_level: RiskLevel = Field(description="Risk level assessment")
    decision: FraudDecision = Field(description="Recommended decision")

    # Analysis details
    indicators: List[FraudIndicator] = Field(description="Fraud indicators found")
    risk_factors: List[RiskFactor] = Field(description="Risk factors identified")
    recommendations: List[str] = Field(description="Recommended actions")

    # Metadata
    confidence_score: float = Field(description="Overall confidence in analysis")
    processing_time_ms: float = Field(description="Processing time in milliseconds")
    model_version: str = Field(description="Model version used")
    analysis_timestamp: datetime = Field(description="Analysis timestamp")

    # Detailed scores
    device_risk_score: Optional[float] = Field(None, description="Device-based risk score")
    behavioral_risk_score: Optional[float] = Field(None, description="Behavioral risk score")
    network_risk_score: Optional[float] = Field(None, description="Network-based risk score")
    velocity_risk_score: Optional[float] = Field(None, description="Velocity-based risk score")

    # Additional insights
    primary_indicator: Optional[str] = Field(None, description="Primary fraud indicator")
    geographic_risk: Optional[str] = Field(None, description="Geographic risk assessment")
    device_reputation: Optional[str] = Field(None, description="Device reputation score")

    @validator('fraud_score', 'confidence_score')
    def validate_score_range(cls, v):
        if not 0 <= v <= 1:
            raise ValueError('Score must be between 0 and 1')
        return v


class UserBehaviorAnalysisResponse(BaseModel):
    """Response from user behavior analysis"""
    user_id: str = Field(description="User identifier")
    anomaly_score: float = Field(description="Behavioral anomaly score 0-1")
    behavioral_risk: RiskLevel = Field(description="Behavioral risk level")

    # Pattern analysis
    detected_patterns: List[Dict[str, Any]] = Field(description="Detected behavior patterns")
    anomalies: List[Dict[str, Any]] = Field(description="Behavioral anomalies")
    trend_analysis: Dict[str, Any] = Field(description="Trend analysis results")

    # Comparisons
    peer_comparison: Optional[Dict[str, Any]] = Field(None, description="Peer group comparison")
    historical_comparison: Dict[str, Any] = Field(description="Historical behavior comparison")

    # Insights
    risk_indicators: List[str] = Field(description="Behavioral risk indicators")
    recommendations: List[str] = Field(description="Recommended monitoring actions")

    analysis_timestamp: datetime = Field(description="Analysis timestamp")


class FraudAlert(BaseModel):
    """Fraud alert notification"""
    alert_id: str = Field(description="Alert identifier")
    transaction_id: Optional[str] = Field(None, description="Related transaction ID")
    user_id: Optional[str] = Field(None, description="Related user ID")

    alert_type: str = Field(description="Type of alert")
    fraud_type: FraudType = Field(description="Type of fraud detected")
    severity: str = Field(description="Alert severity")

    fraud_score: float = Field(description="Fraud score that triggered alert")
    description: str = Field(description="Alert description")

    # Response requirements
    requires_immediate_action: bool = Field(description="Requires immediate response")
    suggested_actions: List[str] = Field(description="Suggested response actions")

    # Metadata
    detection_model: str = Field(description="Model that detected the fraud")
    confidence_level: float = Field(description="Detection confidence")
    timestamp: datetime = Field(description="Alert timestamp")

    # Investigation data
    investigation_priority: str = Field(description="Investigation priority level")
    assigned_to: Optional[str] = Field(None, description="Assigned investigator")
    status: str = Field(default="open", description="Alert status")


class FraudPattern(BaseModel):
    """Detected fraud pattern"""
    pattern_id: str = Field(description="Pattern identifier")
    pattern_type: str = Field(description="Type of pattern")
    description: str = Field(description="Pattern description")

    # Pattern characteristics
    frequency: int = Field(description="Pattern occurrence frequency")
    severity: float = Field(description="Pattern severity score")
    confidence: float = Field(description="Pattern detection confidence")

    # Affected entities
    affected_users: List[str] = Field(description="Users affected by pattern")
    affected_transactions: List[str] = Field(description="Transactions involved")
    geographic_scope: Optional[List[str]] = Field(None, description="Geographic areas affected")

    # Timeline
    first_detected: datetime = Field(description="First pattern detection")
    last_updated: datetime = Field(description="Last pattern update")

    # Response
    countermeasures: List[str] = Field(description="Applied countermeasures")
    effectiveness: Optional[float] = Field(None, description="Countermeasure effectiveness")


class RiskAssessmentResponse(BaseModel):
    """Comprehensive risk assessment response"""
    entity_id: str = Field(description="Entity identifier")
    entity_type: EntityType = Field(description="Entity type")

    # Overall assessment
    overall_risk_score: float = Field(description="Overall risk score 0-1")
    risk_level: RiskLevel = Field(description="Risk level")

    # Detailed assessments
    risk_categories: Dict[str, float] = Field(description="Risk scores by category")
    risk_factors: List[RiskFactor] = Field(description="Identified risk factors")

    # Historical context
    risk_trend: str = Field(description="Risk trend (increasing, stable, decreasing)")
    historical_incidents: int = Field(description="Number of historical incidents")

    # Recommendations
    risk_mitigation: List[str] = Field(description="Risk mitigation recommendations")
    monitoring_recommendations: List[str] = Field(description="Monitoring recommendations")

    # Metadata
    assessment_confidence: float = Field(description="Assessment confidence")
    assessment_timestamp: datetime = Field(description="Assessment timestamp")
    data_quality_score: float = Field(description="Input data quality score")


class FraudStatistics(BaseModel):
    """Fraud detection statistics"""
    timeframe: str = Field(description="Statistics timeframe")

    # Volume metrics
    total_transactions_analyzed: int = Field(description="Total transactions analyzed")
    fraud_cases_detected: int = Field(description="Fraud cases detected")
    fraud_rate: float = Field(description="Fraud rate percentage")

    # Performance metrics
    true_positives: int = Field(description="True positive detections")
    false_positives: int = Field(description="False positive detections")
    false_negatives: int = Field(description="False negative detections")
    precision: float = Field(description="Model precision")
    recall: float = Field(description="Model recall")
    f1_score: float = Field(description="F1 score")

    # Financial impact
    fraud_amount_detected: Decimal = Field(description="Amount of fraud detected")
    fraud_amount_prevented: Decimal = Field(description="Amount of fraud prevented")
    false_positive_cost: Decimal = Field(description="Cost of false positives")

    # Trends
    fraud_trends: Dict[str, Any] = Field(description="Fraud trend analysis")
    top_fraud_types: List[Dict[str, Any]] = Field(description="Top fraud types by frequency")

    # System performance
    average_processing_time: float = Field(description="Average processing time")
    system_availability: float = Field(description="System availability percentage")

    generated_at: datetime = Field(description="Statistics generation timestamp")