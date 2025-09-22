"""
Azure Service Bus client for Search Service
"""

import asyncio
import json
from typing import Any, Dict, Optional
from azure.servicebus.aio import ServiceBusClient, ServiceBusReceiver, ServiceBusSender
from azure.servicebus import ServiceBusMessage
import structlog

from app.core.config import get_settings

settings = get_settings()
logger = structlog.get_logger(__name__)

# Global Service Bus client
service_bus_client: Optional[ServiceBusClient] = None


async def init_service_bus() -> None:
    """Initialize Service Bus connection"""
    global service_bus_client

    if not settings.AZURE_SERVICE_BUS_CONNECTION_STRING:
        logger.warning("No Service Bus connection string configured")
        return

    try:
        service_bus_client = ServiceBusClient.from_connection_string(
            settings.AZURE_SERVICE_BUS_CONNECTION_STRING
        )

        logger.info("Service Bus initialized successfully")

    except Exception as e:
        logger.error("Failed to initialize Service Bus", error=str(e))
        raise


async def close_service_bus() -> None:
    """Close Service Bus connection"""
    global service_bus_client

    if service_bus_client:
        await service_bus_client.close()
        logger.info("Service Bus connection closed")


def get_service_bus() -> ServiceBusClient:
    """Get Service Bus client"""
    if not service_bus_client:
        raise RuntimeError("Service Bus not initialized")
    return service_bus_client


async def send_message(queue_name: str, message_data: Dict[str, Any]) -> bool:
    """Send message to Service Bus queue"""
    try:
        client = get_service_bus()
        async with client:
            sender = client.get_queue_sender(queue_name=queue_name)
            async with sender:
                message = ServiceBusMessage(json.dumps(message_data))
                await sender.send_messages(message)

        logger.info("Message sent successfully", queue=queue_name)
        return True

    except Exception as e:
        logger.error("Failed to send message", queue=queue_name, error=str(e))
        return False


async def receive_messages(queue_name: str, max_messages: int = 10) -> list:
    """Receive messages from Service Bus queue"""
    try:
        client = get_service_bus()
        messages = []

        async with client:
            receiver = client.get_queue_receiver(queue_name=queue_name)
            async with receiver:
                received_msgs = await receiver.receive_messages(max_message_count=max_messages)
                for msg in received_msgs:
                    try:
                        message_data = json.loads(str(msg))
                        messages.append(message_data)
                        await receiver.complete_message(msg)
                    except Exception as e:
                        logger.error("Failed to process message", error=str(e))
                        await receiver.abandon_message(msg)

        return messages

    except Exception as e:
        logger.error("Failed to receive messages", queue=queue_name, error=str(e))
        return []


async def health_check() -> bool:
    """Check Service Bus health"""
    try:
        if not service_bus_client:
            return False

        # Try to get queue properties to verify connection
        async with service_bus_client:
            queue_runtime_props = await service_bus_client.get_queue_runtime_properties("test-queue")
            return True
    except Exception as e:
        logger.error("Service Bus health check failed", error=str(e))
        return False