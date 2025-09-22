"""
Integration tests for the complete order workflow across services
"""

import pytest
import asyncio
import httpx
import json
from datetime import datetime, timedelta
from typing import Dict, Any
import uuid


@pytest.fixture
def test_client():
    """HTTP client for testing"""
    return httpx.AsyncClient(timeout=30.0)


@pytest.fixture
def test_user():
    """Test user data"""
    return {
        "user_id": str(uuid.uuid4()),
        "email": "test@example.com",
        "first_name": "Test",
        "last_name": "User",
        "address": {
            "street": "123 Test St",
            "city": "Test City",
            "state": "TS",
            "zip": "12345",
            "country": "US"
        }
    }


@pytest.fixture
def test_products():
    """Test product data"""
    return [
        {
            "product_id": str(uuid.uuid4()),
            "name": "Test Product 1",
            "price": 29.99,
            "sku": "TEST001",
            "stock": 100
        },
        {
            "product_id": str(uuid.uuid4()),
            "name": "Test Product 2",
            "price": 49.99,
            "sku": "TEST002",
            "stock": 50
        }
    ]


class TestOrderWorkflow:
    """Integration tests for complete order workflow"""

    @pytest.mark.asyncio
    async def test_complete_order_flow(self, test_client, test_user, test_products):
        """Test complete order flow from creation to completion"""

        # Step 1: Create user
        user_response = await test_client.post(
            "http://localhost:5001/api/users",
            json=test_user
        )
        assert user_response.status_code == 201
        created_user = user_response.json()

        # Step 2: Add products to catalog
        product_ids = []
        for product in test_products:
            product_response = await test_client.post(
                "http://localhost:5002/api/products",
                json=product
            )
            assert product_response.status_code == 201
            product_ids.append(product["product_id"])

        # Step 3: Create order
        order_data = {
            "customer_id": test_user["user_id"],
            "items": [
                {
                    "product_id": test_products[0]["product_id"],
                    "quantity": 2,
                    "price": test_products[0]["price"]
                },
                {
                    "product_id": test_products[1]["product_id"],
                    "quantity": 1,
                    "price": test_products[1]["price"]
                }
            ]
        }

        order_response = await test_client.post(
            "http://localhost:5000/api/orders",
            json=order_data
        )
        assert order_response.status_code == 201
        created_order = order_response.json()
        order_id = created_order["id"]

        # Step 4: Verify order creation
        assert created_order["customer_id"] == test_user["user_id"]
        assert len(created_order["items"]) == 2
        assert created_order["status"] == "Pending"
        assert created_order["total_amount"] == 109.97  # (29.99 * 2) + 49.99

        # Step 5: Process payment
        payment_data = {
            "order_id": order_id,
            "amount": created_order["total_amount"],
            "payment_method": "credit_card",
            "card_token": "test_card_token_12345"
        }

        payment_response = await test_client.post(
            "http://localhost:5003/api/payments",
            json=payment_data
        )
        assert payment_response.status_code == 201
        payment_result = payment_response.json()
        assert payment_result["status"] == "Completed"

        # Step 6: Verify order status updated to Paid
        await asyncio.sleep(2)  # Allow for event processing

        order_status_response = await test_client.get(
            f"http://localhost:5000/api/orders/{order_id}"
        )
        assert order_status_response.status_code == 200
        updated_order = order_status_response.json()
        assert updated_order["status"] == "Paid"

        # Step 7: Verify inventory reservation
        for i, product in enumerate(test_products):
            inventory_response = await test_client.get(
                f"http://localhost:5002/api/products/{product['product_id']}/inventory"
            )
            assert inventory_response.status_code == 200
            inventory_data = inventory_response.json()

            expected_quantity = 2 if i == 0 else 1
            expected_stock = product["stock"] - expected_quantity
            assert inventory_data["available_stock"] == expected_stock

        # Step 8: Ship order
        shipping_data = {
            "order_id": order_id,
            "tracking_number": "TEST123456789",
            "carrier": "Test Carrier",
            "estimated_delivery": (datetime.utcnow() + timedelta(days=3)).isoformat()
        }

        shipping_response = await test_client.post(
            f"http://localhost:5000/api/orders/{order_id}/ship",
            json=shipping_data
        )
        assert shipping_response.status_code == 200

        # Step 9: Verify final order status
        final_order_response = await test_client.get(
            f"http://localhost:5000/api/orders/{order_id}"
        )
        assert final_order_response.status_code == 200
        final_order = final_order_response.json()
        assert final_order["status"] == "Shipped"
        assert final_order["tracking_number"] == "TEST123456789"

        # Step 10: Verify notification was sent
        notification_response = await test_client.get(
            f"http://localhost:5004/api/notifications/user/{test_user['user_id']}"
        )
        assert notification_response.status_code == 200
        notifications = notification_response.json()

        # Should have notifications for order confirmation, payment, and shipping
        assert len(notifications) >= 3

    @pytest.mark.asyncio
    async def test_order_cancellation_flow(self, test_client, test_user, test_products):
        """Test order cancellation and refund flow"""

        # Create order first (abbreviated)
        order_data = {
            "customer_id": test_user["user_id"],
            "items": [
                {
                    "product_id": test_products[0]["product_id"],
                    "quantity": 1,
                    "price": test_products[0]["price"]
                }
            ]
        }

        order_response = await test_client.post(
            "http://localhost:5000/api/orders",
            json=order_data
        )
        assert order_response.status_code == 201
        order_id = order_response.json()["id"]

        # Process payment
        payment_data = {
            "order_id": order_id,
            "amount": test_products[0]["price"],
            "payment_method": "credit_card",
            "card_token": "test_card_token_12345"
        }

        await test_client.post("http://localhost:5003/api/payments", json=payment_data)
        await asyncio.sleep(1)  # Allow event processing

        # Cancel order
        cancellation_data = {
            "reason": "Customer requested cancellation",
            "refund_amount": test_products[0]["price"]
        }

        cancel_response = await test_client.post(
            f"http://localhost:5000/api/orders/{order_id}/cancel",
            json=cancellation_data
        )
        assert cancel_response.status_code == 200

        # Verify order status
        order_status_response = await test_client.get(
            f"http://localhost:5000/api/orders/{order_id}"
        )
        assert order_status_response.status_code == 200
        cancelled_order = order_status_response.json()
        assert cancelled_order["status"] == "Cancelled"

        # Verify refund was processed
        await asyncio.sleep(2)  # Allow for event processing

        refund_response = await test_client.get(
            f"http://localhost:5003/api/payments/order/{order_id}/refunds"
        )
        assert refund_response.status_code == 200
        refunds = refund_response.json()
        assert len(refunds) == 1
        assert refunds[0]["amount"] == test_products[0]["price"]
        assert refunds[0]["status"] == "Completed"

    @pytest.mark.asyncio
    async def test_fraud_detection_integration(self, test_client, test_user):
        """Test fraud detection integration in order flow"""

        # Create suspicious order (high value, unusual pattern)
        suspicious_order = {
            "customer_id": test_user["user_id"],
            "items": [
                {
                    "product_id": str(uuid.uuid4()),
                    "quantity": 10,
                    "price": 999.99
                }
            ],
            "metadata": {
                "ip_address": "192.168.1.100",
                "user_agent": "SuspiciousBot/1.0",
                "device_fingerprint": "suspicious_device_123"
            }
        }

        order_response = await test_client.post(
            "http://localhost:5000/api/orders",
            json=suspicious_order
        )
        assert order_response.status_code == 201
        order_id = order_response.json()["id"]

        # Attempt payment
        payment_data = {
            "order_id": order_id,
            "amount": 9999.90,
            "payment_method": "credit_card",
            "card_token": "test_card_token_12345"
        }

        payment_response = await test_client.post(
            "http://localhost:5003/api/payments",
            json=payment_data
        )

        # Payment might be blocked or flagged for review
        assert payment_response.status_code in [200, 202, 400]

        if payment_response.status_code == 202:
            # Payment is under review
            payment_result = payment_response.json()
            assert payment_result["status"] in ["Pending", "UnderReview"]

        # Check fraud detection results
        await asyncio.sleep(3)  # Allow for ML processing

        fraud_response = await test_client.get(
            f"http://localhost:8003/api/v1/fraud/order/{order_id}"
        )

        if fraud_response.status_code == 200:
            fraud_result = fraud_response.json()
            # Fraud service should have flagged this order
            assert fraud_result["risk_score"] > 0.5
            assert fraud_result["risk_level"] in ["Medium", "High", "Critical"]

    @pytest.mark.asyncio
    async def test_recommendation_engine_integration(self, test_client, test_user, test_products):
        """Test recommendation engine integration"""

        # Simulate user viewing products
        for product in test_products:
            view_data = {
                "user_id": test_user["user_id"],
                "product_id": product["product_id"],
                "view_duration": 30,
                "source": "search"
            }

            await test_client.post(
                "http://localhost:8001/api/v1/interactions/view",
                json=view_data
            )

        # Get recommendations
        recommendations_response = await test_client.get(
            f"http://localhost:8001/api/v1/recommendations/{test_user['user_id']}"
        )

        if recommendations_response.status_code == 200:
            recommendations = recommendations_response.json()
            assert len(recommendations["recommendations"]) > 0

            # Recommendations should include relevant products
            recommended_ids = [r["product_id"] for r in recommendations["recommendations"]]
            assert any(pid in recommended_ids for pid in [p["product_id"] for p in test_products])

    @pytest.mark.asyncio
    async def test_search_integration(self, test_client, test_products):
        """Test search service integration"""

        # Index products in search service
        for product in test_products:
            index_data = {
                "product_data": {
                    "id": product["product_id"],
                    "name": product["name"],
                    "price": product["price"],
                    "sku": product["sku"],
                    "description": f"Description for {product['name']}"
                }
            }

            await test_client.post(
                "http://localhost:8004/api/v1/indexing/product",
                json=index_data
            )

        await asyncio.sleep(2)  # Allow for indexing

        # Search for products
        search_data = {
            "query": "Test Product",
            "filters": {},
            "pagination": {"page": 1, "size": 10}
        }

        search_response = await test_client.post(
            "http://localhost:8004/api/v1/search",
            json=search_data
        )

        if search_response.status_code == 200:
            search_results = search_response.json()
            assert search_results["total_results"] >= 2
            assert len(search_results["products"]) >= 2

            # Verify product data in search results
            result_names = [p["name"] for p in search_results["products"]]
            assert "Test Product 1" in result_names
            assert "Test Product 2" in result_names

    @pytest.mark.asyncio
    async def test_notification_flow(self, test_client, test_user):
        """Test notification service integration"""

        # Send test notification
        notification_data = {
            "user_id": test_user["user_id"],
            "notification_type": "order_confirmation",
            "channel": "email",
            "subject": "Order Confirmation",
            "message": "Your order has been confirmed",
            "data": {
                "order_id": str(uuid.uuid4()),
                "amount": 99.99
            }
        }

        notification_response = await test_client.post(
            "http://localhost:5004/api/notifications/send",
            json=notification_data
        )
        assert notification_response.status_code == 200

        # Verify notification was queued/sent
        sent_notifications = await test_client.get(
            f"http://localhost:5004/api/notifications/user/{test_user['user_id']}"
        )
        assert sent_notifications.status_code == 200
        notifications = sent_notifications.json()
        assert len(notifications) >= 1

    @pytest.mark.asyncio
    async def test_system_health_checks(self, test_client):
        """Test that all services are healthy"""

        services = [
            ("Order Service", "http://localhost:5000/health"),
            ("User Service", "http://localhost:5001/health"),
            ("Catalog Service", "http://localhost:5002/health"),
            ("Payment Service", "http://localhost:5003/health"),
            ("Notification Service", "http://localhost:5004/health"),
            ("Recommendation Engine", "http://localhost:8001/health"),
            ("Price Optimization", "http://localhost:8002/health"),
            ("Fraud Detection", "http://localhost:8003/health"),
            ("Inventory Analytics", "http://localhost:8004/health"),
            ("Search Service", "http://localhost:8005/health"),
        ]

        for service_name, health_url in services:
            try:
                health_response = await test_client.get(health_url)
                assert health_response.status_code == 200, f"{service_name} health check failed"

                health_data = health_response.json()
                assert health_data.get("status") in ["healthy", "Healthy", "OK"], f"{service_name} is not healthy"

            except Exception as e:
                pytest.fail(f"{service_name} health check failed with error: {str(e)}")


# Utility functions for test data cleanup
@pytest.fixture(autouse=True)
async def cleanup_test_data():
    """Clean up test data after each test"""
    yield
    # Add cleanup logic here if needed
    # This could include deleting test orders, users, etc.
    pass


if __name__ == "__main__":
    # Run integration tests
    pytest.main([__file__, "-v", "--asyncio-mode=auto"])