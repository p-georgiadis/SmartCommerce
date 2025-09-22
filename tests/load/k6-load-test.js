/**
 * SmartCommerce Load Testing Script
 *
 * This script performs comprehensive load testing across all SmartCommerce services
 * using k6 (https://k6.io/)
 *
 * Usage:
 * k6 run --vus 50 --duration 5m k6-load-test.js
 */

import http from 'k6/http';
import { check, group, sleep } from 'k6';
import { Rate, Trend, Counter } from 'k6/metrics';
import { randomString, randomIntBetween } from 'https://jslib.k6.io/k6-utils/1.2.0/index.js';

// Custom metrics
export const errorRate = new Rate('errors');
export const orderCreationTime = new Trend('order_creation_time');
export const searchResponseTime = new Trend('search_response_time');
export const recommendationTime = new Trend('recommendation_time');
export const paymentProcessingTime = new Trend('payment_processing_time');
export const apiCallsCounter = new Counter('api_calls_total');

// Test configuration
export const options = {
  // Load testing stages
  stages: [
    { duration: '2m', target: 20 },  // Ramp up to 20 users
    { duration: '5m', target: 50 },  // Stay at 50 users
    { duration: '2m', target: 100 }, // Ramp up to 100 users
    { duration: '5m', target: 100 }, // Stay at 100 users
    { duration: '2m', target: 0 },   // Ramp down to 0 users
  ],

  // Thresholds for success criteria
  thresholds: {
    http_req_duration: ['p(95)<2000'], // 95% of requests must complete under 2s
    http_req_failed: ['rate<0.1'],     // Error rate must be less than 10%
    errors: ['rate<0.1'],              // Custom error rate
    order_creation_time: ['p(95)<3000'], // Order creation under 3s
    search_response_time: ['p(95)<1000'], // Search under 1s
    recommendation_time: ['p(95)<2000'],  // Recommendations under 2s
    payment_processing_time: ['p(95)<5000'], // Payment under 5s
  },
};

// Base URLs for services
const BASE_URLS = {
  ORDER_SERVICE: __ENV.ORDER_SERVICE_URL || 'http://localhost:5000',
  USER_SERVICE: __ENV.USER_SERVICE_URL || 'http://localhost:5001',
  CATALOG_SERVICE: __ENV.CATALOG_SERVICE_URL || 'http://localhost:5002',
  PAYMENT_SERVICE: __ENV.PAYMENT_SERVICE_URL || 'http://localhost:5003',
  NOTIFICATION_SERVICE: __ENV.NOTIFICATION_SERVICE_URL || 'http://localhost:5004',
  RECOMMENDATION_ENGINE: __ENV.RECOMMENDATION_URL || 'http://localhost:8001',
  PRICE_OPTIMIZATION: __ENV.PRICE_OPTIMIZATION_URL || 'http://localhost:8002',
  FRAUD_DETECTION: __ENV.FRAUD_DETECTION_URL || 'http://localhost:8003',
  INVENTORY_ANALYTICS: __ENV.INVENTORY_ANALYTICS_URL || 'http://localhost:8004',
  SEARCH_SERVICE: __ENV.SEARCH_SERVICE_URL || 'http://localhost:8005',
};

// Test data generators
function generateUser() {
  return {
    user_id: randomString(10),
    email: `user${randomString(5)}@example.com`,
    first_name: `FirstName${randomString(3)}`,
    last_name: `LastName${randomString(3)}`,
    address: {
      street: `${randomIntBetween(1, 999)} Test St`,
      city: 'Test City',
      state: 'TS',
      zip: `${randomIntBetween(10000, 99999)}`,
      country: 'US'
    }
  };
}

function generateProduct() {
  return {
    product_id: randomString(10),
    name: `Product ${randomString(5)}`,
    description: `Description for product ${randomString(10)}`,
    price: randomIntBetween(10, 1000) + 0.99,
    sku: `SKU${randomString(8)}`,
    category: ['Electronics', 'Clothing', 'Books', 'Home'][randomIntBetween(0, 3)],
    brand: `Brand${randomString(4)}`,
    stock: randomIntBetween(1, 100)
  };
}

function generateOrder(customerId, productIds) {
  const itemCount = randomIntBetween(1, 3);
  const items = [];

  for (let i = 0; i < itemCount; i++) {
    const productId = productIds[randomIntBetween(0, productIds.length - 1)];
    items.push({
      product_id: productId,
      quantity: randomIntBetween(1, 5),
      price: randomIntBetween(10, 100) + 0.99
    });
  }

  return {
    customer_id: customerId,
    items: items
  };
}

function generatePayment(orderId, amount) {
  return {
    order_id: orderId,
    amount: amount,
    payment_method: 'credit_card',
    card_token: `token_${randomString(12)}`
  };
}

// Setup function - runs once per VU
export function setup() {
  console.log('Setting up test data...');

  // Create some test products for the load test
  const products = [];
  for (let i = 0; i < 20; i++) {
    products.push(generateProduct());
  }

  return { products };
}

// Main test function
export default function(data) {
  const products = data.products;
  const user = generateUser();

  // Health checks for all services
  group('Health Checks', function() {
    Object.entries(BASE_URLS).forEach(([serviceName, baseUrl]) => {
      const response = http.get(`${baseUrl}/health`, {
        timeout: '10s',
        tags: { service: serviceName, operation: 'health_check' }
      });

      check(response, {
        [`${serviceName} health check successful`]: (r) => r.status === 200,
      }) || errorRate.add(1);

      apiCallsCounter.add(1);
    });
  });

  // User Management Load Test
  group('User Management', function() {
    // Create user
    const userResponse = http.post(
      `${BASE_URLS.USER_SERVICE}/api/users`,
      JSON.stringify(user),
      {
        headers: { 'Content-Type': 'application/json' },
        tags: { service: 'UserService', operation: 'create_user' }
      }
    );

    check(userResponse, {
      'User created successfully': (r) => r.status === 201,
      'User response has ID': (r) => JSON.parse(r.body).id !== undefined,
    }) || errorRate.add(1);

    apiCallsCounter.add(1);

    if (userResponse.status === 201) {
      const createdUser = JSON.parse(userResponse.body);

      // Get user profile
      const profileResponse = http.get(
        `${BASE_URLS.USER_SERVICE}/api/users/${createdUser.id}`,
        {
          tags: { service: 'UserService', operation: 'get_user' }
        }
      );

      check(profileResponse, {
        'User profile retrieved': (r) => r.status === 200,
      }) || errorRate.add(1);

      apiCallsCounter.add(1);
    }
  });

  // Product Catalog Load Test
  group('Product Catalog', function() {
    // Get random products
    const catalogResponse = http.get(
      `${BASE_URLS.CATALOG_SERVICE}/api/products?page=1&pageSize=10`,
      {
        tags: { service: 'CatalogService', operation: 'get_products' }
      }
    );

    check(catalogResponse, {
      'Product catalog retrieved': (r) => r.status === 200,
      'Catalog has products': (r) => {
        try {
          const body = JSON.parse(r.body);
          return body.products && body.products.length > 0;
        } catch {
          return false;
        }
      },
    }) || errorRate.add(1);

    apiCallsCounter.add(1);

    // Get specific product
    if (products.length > 0) {
      const randomProduct = products[randomIntBetween(0, products.length - 1)];
      const productResponse = http.get(
        `${BASE_URLS.CATALOG_SERVICE}/api/products/${randomProduct.product_id}`,
        {
          tags: { service: 'CatalogService', operation: 'get_product' }
        }
      );

      check(productResponse, {
        'Product retrieved': (r) => r.status === 200 || r.status === 404, // 404 is acceptable for test data
      }) || errorRate.add(1);

      apiCallsCounter.add(1);
    }
  });

  // Search Service Load Test
  group('Search Service', function() {
    const searchQueries = ['laptop', 'phone', 'book', 'shirt', 'electronics'];
    const query = searchQueries[randomIntBetween(0, searchQueries.length - 1)];

    const searchData = {
      query: query,
      filters: {},
      pagination: { page: 1, size: 20 }
    };

    const searchStart = Date.now();
    const searchResponse = http.post(
      `${BASE_URLS.SEARCH_SERVICE}/api/v1/search`,
      JSON.stringify(searchData),
      {
        headers: { 'Content-Type': 'application/json' },
        tags: { service: 'SearchService', operation: 'search' }
      }
    );
    const searchEnd = Date.now();

    searchResponseTime.add(searchEnd - searchStart);

    check(searchResponse, {
      'Search completed': (r) => r.status === 200,
      'Search has results': (r) => {
        try {
          const body = JSON.parse(r.body);
          return body.total_results !== undefined;
        } catch {
          return false;
        }
      },
    }) || errorRate.add(1);

    apiCallsCounter.add(1);

    // Test autocomplete
    const autocompleteResponse = http.get(
      `${BASE_URLS.SEARCH_SERVICE}/api/v1/search/autocomplete?q=${query.substring(0, 3)}`,
      {
        tags: { service: 'SearchService', operation: 'autocomplete' }
      }
    );

    check(autocompleteResponse, {
      'Autocomplete works': (r) => r.status === 200,
    }) || errorRate.add(1);

    apiCallsCounter.add(1);
  });

  // Recommendation Engine Load Test
  group('Recommendation Engine', function() {
    const recStart = Date.now();
    const recommendationResponse = http.get(
      `${BASE_URLS.RECOMMENDATION_ENGINE}/api/v1/recommendations/${user.user_id}?count=10`,
      {
        tags: { service: 'RecommendationEngine', operation: 'get_recommendations' }
      }
    );
    const recEnd = Date.now();

    recommendationTime.add(recEnd - recStart);

    check(recommendationResponse, {
      'Recommendations retrieved': (r) => r.status === 200,
    }) || errorRate.add(1);

    apiCallsCounter.add(1);

    // Simulate user interaction
    if (products.length > 0) {
      const randomProduct = products[randomIntBetween(0, products.length - 1)];
      const interactionData = {
        user_id: user.user_id,
        product_id: randomProduct.product_id,
        action: 'view',
        rating: randomIntBetween(1, 5)
      };

      const feedbackResponse = http.post(
        `${BASE_URLS.RECOMMENDATION_ENGINE}/api/v1/recommendations/feedback`,
        JSON.stringify(interactionData),
        {
          headers: { 'Content-Type': 'application/json' },
          tags: { service: 'RecommendationEngine', operation: 'feedback' }
        }
      );

      check(feedbackResponse, {
        'Feedback processed': (r) => r.status === 200,
      }) || errorRate.add(1);

      apiCallsCounter.add(1);
    }
  });

  // Order Processing Load Test
  group('Order Processing', function() {
    const productIds = products.map(p => p.product_id);
    const orderData = generateOrder(user.user_id, productIds);

    const orderStart = Date.now();
    const orderResponse = http.post(
      `${BASE_URLS.ORDER_SERVICE}/api/orders`,
      JSON.stringify(orderData),
      {
        headers: { 'Content-Type': 'application/json' },
        tags: { service: 'OrderService', operation: 'create_order' }
      }
    );
    const orderEnd = Date.now();

    orderCreationTime.add(orderEnd - orderStart);

    check(orderResponse, {
      'Order created successfully': (r) => r.status === 201,
      'Order has ID': (r) => {
        try {
          const body = JSON.parse(r.body);
          return body.id !== undefined;
        } catch {
          return false;
        }
      },
    }) || errorRate.add(1);

    apiCallsCounter.add(1);

    if (orderResponse.status === 201) {
      const createdOrder = JSON.parse(orderResponse.body);

      // Process payment
      const paymentData = generatePayment(createdOrder.id, createdOrder.total_amount);

      const paymentStart = Date.now();
      const paymentResponse = http.post(
        `${BASE_URLS.PAYMENT_SERVICE}/api/payments`,
        JSON.stringify(paymentData),
        {
          headers: { 'Content-Type': 'application/json' },
          tags: { service: 'PaymentService', operation: 'process_payment' }
        }
      );
      const paymentEnd = Date.now();

      paymentProcessingTime.add(paymentEnd - paymentStart);

      check(paymentResponse, {
        'Payment processed': (r) => r.status === 201 || r.status === 200,
      }) || errorRate.add(1);

      apiCallsCounter.add(1);

      // Check fraud detection
      sleep(1); // Allow fraud detection to process

      const fraudResponse = http.get(
        `${BASE_URLS.FRAUD_DETECTION}/api/v1/fraud/order/${createdOrder.id}`,
        {
          tags: { service: 'FraudDetection', operation: 'check_fraud' }
        }
      );

      check(fraudResponse, {
        'Fraud check completed': (r) => r.status === 200 || r.status === 404,
      }) || errorRate.add(1);

      apiCallsCounter.add(1);
    }
  });

  // Price Optimization Load Test
  group('Price Optimization', function() {
    if (products.length > 0) {
      const randomProduct = products[randomIntBetween(0, products.length - 1)];
      const priceOptimizationData = {
        product_ids: [randomProduct.product_id],
        market_factors: {
          demand: randomIntBetween(1, 10),
          competition: randomIntBetween(1, 5),
          inventory_level: randomIntBetween(1, 100)
        }
      };

      const priceResponse = http.post(
        `${BASE_URLS.PRICE_OPTIMIZATION}/api/v1/optimize-prices`,
        JSON.stringify(priceOptimizationData),
        {
          headers: { 'Content-Type': 'application/json' },
          tags: { service: 'PriceOptimization', operation: 'optimize_prices' }
        }
      );

      check(priceResponse, {
        'Price optimization completed': (r) => r.status === 200,
      }) || errorRate.add(1);

      apiCallsCounter.add(1);
    }
  });

  // Inventory Analytics Load Test
  group('Inventory Analytics', function() {
    const inventoryResponse = http.get(
      `${BASE_URLS.INVENTORY_ANALYTICS}/api/v1/inventory-dashboard`,
      {
        tags: { service: 'InventoryAnalytics', operation: 'get_dashboard' }
      }
    );

    check(inventoryResponse, {
      'Inventory dashboard retrieved': (r) => r.status === 200,
    }) || errorRate.add(1);

    apiCallsCounter.add(1);

    // Demand forecasting
    if (products.length > 0) {
      const randomProduct = products[randomIntBetween(0, products.length - 1)];
      const forecastData = {
        product_ids: [randomProduct.product_id],
        forecast_horizon: 30,
        granularity: 'daily'
      };

      const forecastResponse = http.post(
        `${BASE_URLS.INVENTORY_ANALYTICS}/api/v1/forecast-demand`,
        JSON.stringify(forecastData),
        {
          headers: { 'Content-Type': 'application/json' },
          tags: { service: 'InventoryAnalytics', operation: 'forecast_demand' }
        }
      );

      check(forecastResponse, {
        'Demand forecast completed': (r) => r.status === 200,
      }) || errorRate.add(1);

      apiCallsCounter.add(1);
    }
  });

  // Add some realistic delays between operations
  sleep(randomIntBetween(1, 3));
}

// Teardown function - runs once after all VUs complete
export function teardown(data) {
  console.log('Load test completed');
  console.log(`Total API calls made: ${apiCallsCounter.value}`);

  // Log final metrics
  console.log('Final Metrics Summary:');
  console.log(`- Average order creation time: ${orderCreationTime.avg}ms`);
  console.log(`- Average search response time: ${searchResponseTime.avg}ms`);
  console.log(`- Average recommendation time: ${recommendationTime.avg}ms`);
  console.log(`- Average payment processing time: ${paymentProcessingTime.avg}ms`);
  console.log(`- Error rate: ${(errorRate.rate * 100).toFixed(2)}%`);
}