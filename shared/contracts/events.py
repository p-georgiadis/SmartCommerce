"""
Shared event contracts for SmartCommerce Python services

This module defines the event schemas used across all Python microservices
for event-driven communication via Azure Service Bus.
"""

from datetime import datetime
from typing import Dict, List, Optional, Any
from dataclasses import dataclass, field
from enum import Enum
import uuid


class EventType(str, Enum):
    """Event type enumeration"""
    # Order events
    ORDER_CREATED = "OrderCreated"
    ORDER_UPDATED = "OrderUpdated"
    ORDER_CANCELLED = "OrderCancelled"
    ORDER_SHIPPED = "OrderShipped"

    # Payment events
    PAYMENT_PROCESSED = "PaymentProcessed"
    PAYMENT_FAILED = "PaymentFailed"
    REFUND_PROCESSED = "RefundProcessed"

    # User events
    USER_REGISTERED = "UserRegistered"
    USER_PROFILE_UPDATED = "UserProfileUpdated"
    USER_PREFERENCES_UPDATED = "UserPreferencesUpdated"

    # Product events
    PRODUCT_CREATED = "ProductCreated"
    PRODUCT_UPDATED = "ProductUpdated"
    PRODUCT_PRICE_CHANGED = "ProductPriceChanged"
    PRODUCT_STOCK_UPDATED = "ProductStockUpdated"

    # Inventory events
    INVENTORY_RESERVED = "InventoryReserved"
    INVENTORY_RELEASED = "InventoryReleased"
    STOCK_LOW_ALERT = "StockLowAlert"

    # Notification events
    NOTIFICATION_REQUESTED = "NotificationRequested"
    NOTIFICATION_SENT = "NotificationSent"

    # Search and Analytics events
    SEARCH_PERFORMED = "SearchPerformed"
    PRODUCT_VIEWED = "ProductViewed"
    RECOMMENDATION_GENERATED = "RecommendationGenerated"

    # Fraud Detection events
    FRAUD_SUSPICION = "FraudSuspicion"
    FRAUD_CONFIRMED = "FraudConfirmed"


@dataclass
class DomainEvent:
    """Base class for all domain events"""
    event_id: str = field(default_factory=lambda: str(uuid.uuid4()))
    event_type: str = field(default="")
    occurred_at: datetime = field(default_factory=datetime.utcnow)
    version: int = 1
    metadata: Dict[str, Any] = field(default_factory=dict)

    def __post_init__(self):
        if not self.event_type:
            self.event_type = self.__class__.__name__


@dataclass
class OrderItemEvent:
    """Order item data for events"""
    product_id: str
    quantity: int
    price: float
    product_name: str = ""
    product_sku: str = ""


@dataclass
class OrderCreatedEvent(DomainEvent):
    """Order created event"""
    order_id: str = ""
    customer_id: str = ""
    total_amount: float = 0.0
    currency: str = "USD"
    items: List[OrderItemEvent] = field(default_factory=list)
    order_date: datetime = field(default_factory=datetime.utcnow)
    status: str = "Pending"

    def __post_init__(self):
        super().__post_init__()
        self.event_type = EventType.ORDER_CREATED


@dataclass
class OrderUpdatedEvent(DomainEvent):
    """Order updated event"""
    order_id: str = ""
    previous_status: str = ""
    new_status: str = ""
    updated_at: datetime = field(default_factory=datetime.utcnow)
    updated_by: str = ""
    reason: str = ""

    def __post_init__(self):
        super().__post_init__()
        self.event_type = EventType.ORDER_UPDATED


@dataclass
class OrderCancelledEvent(DomainEvent):
    """Order cancelled event"""
    order_id: str = ""
    customer_id: str = ""
    cancellation_reason: str = ""
    refund_amount: float = 0.0
    cancelled_at: datetime = field(default_factory=datetime.utcnow)

    def __post_init__(self):
        super().__post_init__()
        self.event_type = EventType.ORDER_CANCELLED


@dataclass
class OrderShippedEvent(DomainEvent):
    """Order shipped event"""
    order_id: str = ""
    tracking_number: str = ""
    shipping_carrier: str = ""
    shipped_at: datetime = field(default_factory=datetime.utcnow)
    estimated_delivery: Optional[datetime] = None
    shipping_address: str = ""

    def __post_init__(self):
        super().__post_init__()
        self.event_type = EventType.ORDER_SHIPPED


@dataclass
class PaymentProcessedEvent(DomainEvent):
    """Payment processed event"""
    payment_id: str = ""
    order_id: str = ""
    amount: float = 0.0
    currency: str = "USD"
    payment_method: str = ""
    transaction_id: str = ""
    status: str = ""
    processed_at: datetime = field(default_factory=datetime.utcnow)

    def __post_init__(self):
        super().__post_init__()
        self.event_type = EventType.PAYMENT_PROCESSED


@dataclass
class PaymentFailedEvent(DomainEvent):
    """Payment failed event"""
    payment_id: str = ""
    order_id: str = ""
    amount: float = 0.0
    currency: str = "USD"
    failure_reason: str = ""
    error_code: str = ""
    failed_at: datetime = field(default_factory=datetime.utcnow)

    def __post_init__(self):
        super().__post_init__()
        self.event_type = EventType.PAYMENT_FAILED


@dataclass
class UserRegisteredEvent(DomainEvent):
    """User registered event"""
    user_id: str = ""
    email: str = ""
    first_name: str = ""
    last_name: str = ""
    registered_at: datetime = field(default_factory=datetime.utcnow)
    registration_source: str = "Web"

    def __post_init__(self):
        super().__post_init__()
        self.event_type = EventType.USER_REGISTERED


@dataclass
class UserProfileUpdatedEvent(DomainEvent):
    """User profile updated event"""
    user_id: str = ""
    changes: Dict[str, Any] = field(default_factory=dict)
    updated_at: datetime = field(default_factory=datetime.utcnow)

    def __post_init__(self):
        super().__post_init__()
        self.event_type = EventType.USER_PROFILE_UPDATED


@dataclass
class ProductCreatedEvent(DomainEvent):
    """Product created event"""
    product_id: str = ""
    name: str = ""
    description: str = ""
    sku: str = ""
    price: float = 0.0
    currency: str = "USD"
    category: str = ""
    brand: str = ""
    created_at: datetime = field(default_factory=datetime.utcnow)

    def __post_init__(self):
        super().__post_init__()
        self.event_type = EventType.PRODUCT_CREATED


@dataclass
class ProductUpdatedEvent(DomainEvent):
    """Product updated event"""
    product_id: str = ""
    changes: Dict[str, Any] = field(default_factory=dict)
    updated_at: datetime = field(default_factory=datetime.utcnow)

    def __post_init__(self):
        super().__post_init__()
        self.event_type = EventType.PRODUCT_UPDATED


@dataclass
class ProductPriceChangedEvent(DomainEvent):
    """Product price changed event"""
    product_id: str = ""
    old_price: float = 0.0
    new_price: float = 0.0
    currency: str = "USD"
    change_reason: str = ""
    changed_at: datetime = field(default_factory=datetime.utcnow)

    def __post_init__(self):
        super().__post_init__()
        self.event_type = EventType.PRODUCT_PRICE_CHANGED


@dataclass
class ProductStockUpdatedEvent(DomainEvent):
    """Product stock updated event"""
    product_id: str = ""
    old_stock_level: int = 0
    new_stock_level: int = 0
    update_reason: str = ""
    updated_at: datetime = field(default_factory=datetime.utcnow)

    def __post_init__(self):
        super().__post_init__()
        self.event_type = EventType.PRODUCT_STOCK_UPDATED


@dataclass
class InventoryReservedEvent(DomainEvent):
    """Inventory reserved event"""
    product_id: str = ""
    order_id: str = ""
    quantity: int = 0
    reserved_at: datetime = field(default_factory=datetime.utcnow)
    reservation_id: str = ""

    def __post_init__(self):
        super().__post_init__()
        self.event_type = EventType.INVENTORY_RESERVED


@dataclass
class StockLowAlertEvent(DomainEvent):
    """Stock low alert event"""
    product_id: str = ""
    product_name: str = ""
    sku: str = ""
    current_stock: int = 0
    minimum_threshold: int = 0
    alert_triggered_at: datetime = field(default_factory=datetime.utcnow)

    def __post_init__(self):
        super().__post_init__()
        self.event_type = EventType.STOCK_LOW_ALERT


@dataclass
class NotificationRequestedEvent(DomainEvent):
    """Notification requested event"""
    user_id: str = ""
    notification_type: str = ""
    channel: str = ""  # Email, SMS, Push, InApp
    subject: str = ""
    message: str = ""
    data: Dict[str, Any] = field(default_factory=dict)
    requested_at: datetime = field(default_factory=datetime.utcnow)
    priority: int = 1  # 1-5 scale

    def __post_init__(self):
        super().__post_init__()
        self.event_type = EventType.NOTIFICATION_REQUESTED


@dataclass
class SearchPerformedEvent(DomainEvent):
    """Search performed event"""
    query: str = ""
    user_id: Optional[str] = None
    session_id: str = ""
    results_count: int = 0
    search_type: str = "Standard"  # Standard, Semantic, Personalized
    filters: Dict[str, Any] = field(default_factory=dict)
    search_duration_ms: int = 0
    searched_at: datetime = field(default_factory=datetime.utcnow)

    def __post_init__(self):
        super().__post_init__()
        self.event_type = EventType.SEARCH_PERFORMED


@dataclass
class ProductViewedEvent(DomainEvent):
    """Product viewed event"""
    product_id: str = ""
    user_id: Optional[str] = None
    session_id: str = ""
    source: str = ""  # Search, Category, Recommendation
    referrer_url: str = ""
    view_duration_seconds: int = 0
    viewed_at: datetime = field(default_factory=datetime.utcnow)

    def __post_init__(self):
        super().__post_init__()
        self.event_type = EventType.PRODUCT_VIEWED


@dataclass
class RecommendationGeneratedEvent(DomainEvent):
    """Recommendation generated event"""
    user_id: Optional[str] = None
    session_id: str = ""
    recommendation_type: str = ""  # Collaborative, ContentBased, Hybrid
    recommended_product_ids: List[str] = field(default_factory=list)
    context: str = ""  # HomePage, ProductPage, CartPage
    generated_at: datetime = field(default_factory=datetime.utcnow)
    model_parameters: Dict[str, Any] = field(default_factory=dict)

    def __post_init__(self):
        super().__post_init__()
        self.event_type = EventType.RECOMMENDATION_GENERATED


@dataclass
class FraudSuspicionEvent(DomainEvent):
    """Fraud suspicion event"""
    order_id: str = ""
    user_id: Optional[str] = None
    fraud_type: str = ""
    risk_score: float = 0.0
    risk_level: str = ""  # Low, Medium, High, Critical
    trigger_reasons: List[str] = field(default_factory=list)
    evidence: Dict[str, Any] = field(default_factory=dict)
    detected_at: datetime = field(default_factory=datetime.utcnow)
    model_version: str = ""

    def __post_init__(self):
        super().__post_init__()
        self.event_type = EventType.FRAUD_SUSPICION


@dataclass
class FraudConfirmedEvent(DomainEvent):
    """Fraud confirmed event"""
    order_id: str = ""
    user_id: Optional[str] = None
    fraud_type: str = ""
    confirmation_method: str = ""  # Manual, Automatic
    confirmed_by: str = ""
    confirmed_at: datetime = field(default_factory=datetime.utcnow)
    actions: str = ""

    def __post_init__(self):
        super().__post_init__()
        self.event_type = EventType.FRAUD_CONFIRMED


# Event serialization utilities
import json
from datetime import datetime


class EventEncoder(json.JSONEncoder):
    """JSON encoder for events"""

    def default(self, obj):
        if isinstance(obj, datetime):
            return obj.isoformat()
        if hasattr(obj, '__dict__'):
            return obj.__dict__
        return super().default(obj)


def serialize_event(event: DomainEvent) -> str:
    """Serialize event to JSON string"""
    return json.dumps(event, cls=EventEncoder)


def deserialize_event(event_json: str, event_class: type) -> DomainEvent:
    """Deserialize JSON string to event object"""
    data = json.loads(event_json)

    # Convert datetime strings back to datetime objects
    for key, value in data.items():
        if key.endswith('_at') or key == 'occurred_at':
            if isinstance(value, str):
                data[key] = datetime.fromisoformat(value.replace('Z', '+00:00'))

    return event_class(**data)