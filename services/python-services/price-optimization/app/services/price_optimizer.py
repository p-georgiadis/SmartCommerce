"""
Price Optimization Service

This module contains the core price optimization logic using machine learning algorithms
to determine optimal pricing strategies based on various factors including demand,
competition, inventory levels, and market conditions.
"""

import asyncio
import json
import numpy as np
import pandas as pd
from datetime import datetime, timedelta
from typing import Dict, List, Optional, Tuple, Any
from dataclasses import dataclass

import joblib
from sklearn.ensemble import RandomForestRegressor, GradientBoostingRegressor
from sklearn.linear_model import ElasticNet
from sklearn.preprocessing import StandardScaler
from sklearn.model_selection import cross_val_score
from xgboost import XGBRegressor
import lightgbm as lgb

import structlog
from app.core.config import get_settings
from app.models.schemas import (
    PriceOptimizationRequest,
    PriceOptimizationResponse,
    PricingConstraints,
    PriceChangeReason,
    PriceElasticity,
    RevenueProjection,
    PricingInsights,
    ProductInfo,
    MarketCondition,
    PricingStrategy
)

logger = structlog.get_logger()
settings = get_settings()


@dataclass
class ModelPrediction:
    """Model prediction result"""
    price: float
    confidence: float
    features_importance: Dict[str, float]
    model_used: str


class PriceOptimizerService:
    """Price optimization service using machine learning models"""

    def __init__(self):
        self.models: Dict[str, Any] = {}
        self.scalers: Dict[str, StandardScaler] = {}
        self.is_initialized = False

        # Model configurations
        self.model_configs = {
            'random_forest': {
                'class': RandomForestRegressor,
                'params': {
                    'n_estimators': 100,
                    'max_depth': 10,
                    'min_samples_split': 5,
                    'random_state': 42
                }
            },
            'xgboost': {
                'class': XGBRegressor,
                'params': {
                    'n_estimators': 100,
                    'max_depth': 6,
                    'learning_rate': 0.1,
                    'random_state': 42
                }
            },
            'lightgbm': {
                'class': lgb.LGBMRegressor,
                'params': {
                    'n_estimators': 100,
                    'max_depth': 6,
                    'learning_rate': 0.1,
                    'random_state': 42
                }
            },
            'gradient_boosting': {
                'class': GradientBoostingRegressor,
                'params': {
                    'n_estimators': 100,
                    'max_depth': 6,
                    'learning_rate': 0.1,
                    'random_state': 42
                }
            },
            'elastic_net': {
                'class': ElasticNet,
                'params': {
                    'alpha': 0.1,
                    'l1_ratio': 0.5,
                    'random_state': 42
                }
            }
        }

    async def load_models(self):
        """Load pre-trained models"""
        try:
            # Initialize models with default configurations
            for model_name, config in self.model_configs.items():
                self.models[model_name] = config['class'](**config['params'])
                self.scalers[model_name] = StandardScaler()

            # Try to load pre-trained models if they exist
            try:
                await self._load_pretrained_models()
            except FileNotFoundError:
                logger.info("No pre-trained models found, using default models")

            self.is_initialized = True
            logger.info("Price optimization models loaded successfully")

        except Exception as e:
            logger.error("Failed to load price optimization models", error=str(e))
            raise

    async def _load_pretrained_models(self):
        """Load pre-trained models from storage"""
        import os
        model_path = settings.MODEL_STORAGE_PATH

        if os.path.exists(model_path):
            for model_name in self.model_configs.keys():
                model_file = os.path.join(model_path, f"{model_name}_price_optimizer.joblib")
                scaler_file = os.path.join(model_path, f"{model_name}_scaler.joblib")

                if os.path.exists(model_file) and os.path.exists(scaler_file):
                    self.models[model_name] = joblib.load(model_file)
                    self.scalers[model_name] = joblib.load(scaler_file)
                    logger.info(f"Loaded pre-trained model: {model_name}")

    async def optimize_product_price(
        self,
        product_id: str,
        current_price: float,
        cost: float,
        inventory_level: int,
        competitor_prices: List[Dict[str, Any]],
        demand_data: Optional[Dict[str, Any]] = None,
        constraints: Optional[PricingConstraints] = None
    ) -> PriceOptimizationResponse:
        """Optimize price for a single product"""

        logger.info("Starting price optimization", product_id=product_id)

        try:
            # Prepare features for ML models
            features = await self._prepare_features(
                current_price=current_price,
                cost=cost,
                inventory_level=inventory_level,
                competitor_prices=competitor_prices,
                demand_data=demand_data
            )

            # Get predictions from multiple models
            predictions = await self._get_ensemble_prediction(features)

            # Apply business rules and constraints
            optimized_price = await self._apply_business_rules(
                predictions=predictions,
                current_price=current_price,
                cost=cost,
                constraints=constraints
            )

            # Calculate expected impact
            impact_analysis = await self._calculate_impact(
                current_price=current_price,
                optimized_price=optimized_price,
                features=features,
                demand_data=demand_data
            )

            # Analyze price elasticity
            elasticity = await self._analyze_price_elasticity(
                product_id=product_id,
                current_price=current_price,
                demand_data=demand_data
            )

            # Generate revenue projections
            revenue_projections = await self._generate_revenue_projections(
                current_price=current_price,
                cost=cost,
                elasticity=elasticity,
                features=features
            )

            # Determine primary reason for price change
            primary_reason, secondary_reasons = await self._analyze_price_change_reasons(
                current_price=current_price,
                optimized_price=optimized_price,
                features=features,
                competitor_prices=competitor_prices
            )

            # Competitive analysis
            competitor_analysis = await self._analyze_competition(
                optimized_price=optimized_price,
                competitor_prices=competitor_prices
            )

            # Risk assessment
            risk_factors = await self._assess_risks(
                current_price=current_price,
                optimized_price=optimized_price,
                features=features
            )

            # Calculate confidence score
            confidence_score = await self._calculate_confidence_score(
                predictions=predictions,
                features=features,
                demand_data=demand_data
            )

            response = PriceOptimizationResponse(
                product_id=product_id,
                current_price=current_price,
                recommended_price=optimized_price,
                price_change=optimized_price - current_price,
                price_change_percent=((optimized_price - current_price) / current_price) * 100,
                expected_demand_change=impact_analysis['demand_change'],
                expected_revenue_change=impact_analysis['revenue_change'],
                expected_profit_change=impact_analysis['profit_change'],
                confidence_score=confidence_score,
                primary_reason=primary_reason,
                secondary_reasons=secondary_reasons,
                price_elasticity=elasticity,
                revenue_projections=revenue_projections,
                competitor_analysis=competitor_analysis,
                market_position=await self._determine_market_position(optimized_price, competitor_prices),
                risk_factors=risk_factors,
                optimization_timestamp=datetime.utcnow(),
                model_version="ensemble_v1.0",
                data_quality_score=await self._assess_data_quality(features, demand_data)
            )

            logger.info(
                "Price optimization completed",
                product_id=product_id,
                current_price=current_price,
                recommended_price=optimized_price,
                confidence_score=confidence_score
            )

            return response

        except Exception as e:
            logger.error("Price optimization failed", product_id=product_id, error=str(e))
            raise

    async def bulk_optimize_prices(
        self,
        products: List[ProductInfo],
        market_conditions: Optional[MarketCondition] = None,
        constraints: Optional[PricingConstraints] = None
    ) -> Dict[str, PriceOptimizationResponse]:
        """Optimize prices for multiple products"""

        logger.info("Starting bulk price optimization", product_count=len(products))

        results = {}

        # Process products in batches to avoid overwhelming the system
        batch_size = 10
        for i in range(0, len(products), batch_size):
            batch = products[i:i + batch_size]

            # Process batch concurrently
            batch_tasks = []
            for product in batch:
                task = self.optimize_product_price(
                    product_id=product.product_id,
                    current_price=product.current_price,
                    cost=product.cost,
                    inventory_level=product.inventory_level,
                    competitor_prices=[],  # Would be fetched from external service
                    demand_data=product.demand_data.dict() if product.demand_data else None,
                    constraints=product.constraints or constraints
                )
                batch_tasks.append(task)

            # Execute batch
            batch_results = await asyncio.gather(*batch_tasks, return_exceptions=True)

            # Process results
            for j, result in enumerate(batch_results):
                product_id = batch[j].product_id
                if isinstance(result, Exception):
                    logger.error("Failed to optimize product", product_id=product_id, error=str(result))
                else:
                    results[product_id] = result

        logger.info("Bulk price optimization completed", total_products=len(products), successful=len(results))

        return results

    async def get_pricing_insights(self, product_id: str) -> PricingInsights:
        """Get comprehensive pricing insights for a product"""

        # This would typically fetch data from various sources
        # For now, we'll return mock data structure

        current_metrics = {
            "revenue": 50000,
            "units_sold": 1000,
            "profit_margin": 0.25,
            "market_share": 0.15,
            "price_rank": 3
        }

        historical_performance = {
            "revenue_trend": "increasing",
            "sales_velocity": 1.2,
            "price_changes": 5,
            "avg_monthly_revenue": 45000
        }

        # Mock price elasticity
        elasticity = PriceElasticity(
            elasticity_coefficient=-1.2,
            demand_sensitivity="moderate",
            optimal_price_range=(45.0, 55.0),
            confidence_score=0.85
        )

        optimization_opportunities = [
            {
                "opportunity": "Increase price by 5%",
                "expected_impact": "+$2,500 monthly revenue",
                "risk_level": "low",
                "confidence": 0.8
            },
            {
                "opportunity": "Bundle with complementary products",
                "expected_impact": "+15% units sold",
                "risk_level": "medium",
                "confidence": 0.7
            }
        ]

        return PricingInsights(
            product_id=product_id,
            current_metrics=current_metrics,
            historical_performance=historical_performance,
            price_sensitivity_analysis=elasticity,
            competitive_position={"position": "premium", "price_gap": 0.12},
            demand_forecast={"next_30_days": 850, "growth_rate": 0.05},
            optimization_opportunities=optimization_opportunities,
            risk_assessment={"overall_risk": "low", "factors": ["seasonal_demand"]},
            strategic_recommendations=[
                "Consider premium positioning strategy",
                "Monitor competitor pricing weekly",
                "Test dynamic pricing during peak seasons"
            ],
            tactical_actions=[
                "Increase price by 3-5%",
                "Set up automated price monitoring",
                "Implement A/B testing for price points"
            ],
            generated_at=datetime.utcnow()
        )

    async def _prepare_features(
        self,
        current_price: float,
        cost: float,
        inventory_level: int,
        competitor_prices: List[Dict[str, Any]],
        demand_data: Optional[Dict[str, Any]] = None
    ) -> np.ndarray:
        """Prepare features for ML models"""

        features = []

        # Basic product features
        features.extend([
            current_price,
            cost,
            current_price - cost,  # profit
            (current_price - cost) / current_price if current_price > 0 else 0,  # margin
            inventory_level,
            np.log1p(inventory_level),  # log inventory
        ])

        # Competitor features
        if competitor_prices:
            prices = [cp.get('price', 0) for cp in competitor_prices]
            features.extend([
                np.mean(prices),  # avg competitor price
                np.min(prices),   # min competitor price
                np.max(prices),   # max competitor price
                np.std(prices),   # price volatility
                current_price / np.mean(prices) if np.mean(prices) > 0 else 1,  # price ratio
                len(prices),      # number of competitors
            ])
        else:
            features.extend([current_price, current_price, current_price, 0, 1, 0])

        # Demand features
        if demand_data and 'daily_sales' in demand_data:
            sales = demand_data['daily_sales']
            if sales:
                features.extend([
                    np.mean(sales),    # avg daily sales
                    np.std(sales),     # sales volatility
                    sales[-1] if sales else 0,  # recent sales
                    np.trend_coefficient(sales) if len(sales) > 1 else 0,  # sales trend
                ])
            else:
                features.extend([0, 0, 0, 0])
        else:
            features.extend([100, 10, 100, 0])  # default values

        # Time-based features
        now = datetime.utcnow()
        features.extend([
            now.month,           # seasonality
            now.weekday(),       # day of week
            now.hour,            # hour of day
        ])

        return np.array(features).reshape(1, -1)

    async def _get_ensemble_prediction(self, features: np.ndarray) -> List[ModelPrediction]:
        """Get predictions from ensemble of models"""

        predictions = []

        for model_name, model in self.models.items():
            try:
                # Scale features
                scaled_features = self.scalers[model_name].fit_transform(features)

                # Get prediction
                prediction = model.predict(scaled_features)[0]

                # Calculate confidence (simplified)
                confidence = 0.8  # Would use cross-validation or other methods

                # Feature importance (if available)
                feature_importance = {}
                if hasattr(model, 'feature_importances_'):
                    feature_names = [f"feature_{i}" for i in range(features.shape[1])]
                    feature_importance = dict(zip(feature_names, model.feature_importances_))

                predictions.append(ModelPrediction(
                    price=prediction,
                    confidence=confidence,
                    features_importance=feature_importance,
                    model_used=model_name
                ))

            except Exception as e:
                logger.warning(f"Model {model_name} prediction failed", error=str(e))

        return predictions

    async def _apply_business_rules(
        self,
        predictions: List[ModelPrediction],
        current_price: float,
        cost: float,
        constraints: Optional[PricingConstraints] = None
    ) -> float:
        """Apply business rules and constraints to model predictions"""

        # Ensemble prediction (weighted average)
        if not predictions:
            return current_price

        # Weight by confidence
        total_weight = sum(p.confidence for p in predictions)
        if total_weight == 0:
            return current_price

        weighted_price = sum(p.price * p.confidence for p in predictions) / total_weight

        # Apply constraints
        min_price = cost * (1 + settings.MIN_PROFIT_MARGIN)
        max_price_change = current_price * settings.MAX_PRICE_CHANGE_PERCENT

        # Default constraints
        optimized_price = max(min_price, weighted_price)
        optimized_price = max(current_price - max_price_change, optimized_price)
        optimized_price = min(current_price + max_price_change, optimized_price)

        # Apply user-defined constraints
        if constraints:
            if constraints.min_price:
                optimized_price = max(constraints.min_price, optimized_price)
            if constraints.max_price:
                optimized_price = min(constraints.max_price, optimized_price)
            if constraints.min_margin:
                min_price_with_margin = cost / (1 - constraints.min_margin)
                optimized_price = max(min_price_with_margin, optimized_price)
            if constraints.round_to_nearest:
                optimized_price = round(optimized_price / constraints.round_to_nearest) * constraints.round_to_nearest
            if constraints.psychological_pricing:
                optimized_price = round(optimized_price - 0.01, 2)

        return round(optimized_price, 2)

    async def _calculate_impact(
        self,
        current_price: float,
        optimized_price: float,
        features: np.ndarray,
        demand_data: Optional[Dict[str, Any]] = None
    ) -> Dict[str, float]:
        """Calculate expected impact of price change"""

        price_change_percent = ((optimized_price - current_price) / current_price) * 100

        # Simple demand elasticity model (would be more sophisticated in reality)
        elasticity = -1.2  # Default elasticity
        demand_change = elasticity * price_change_percent

        # Revenue and profit calculations
        revenue_change = price_change_percent + demand_change
        current_profit_margin = 0.25  # Would calculate from actual data
        profit_change = revenue_change * (1 + current_profit_margin)

        return {
            'demand_change': demand_change,
            'revenue_change': revenue_change,
            'profit_change': profit_change
        }

    async def _analyze_price_elasticity(
        self,
        product_id: str,
        current_price: float,
        demand_data: Optional[Dict[str, Any]] = None
    ) -> PriceElasticity:
        """Analyze price elasticity of demand"""

        # Simplified elasticity calculation
        # In reality, this would use historical price and demand data

        elasticity_coefficient = -1.2  # Mock value
        optimal_range = (current_price * 0.9, current_price * 1.1)

        if abs(elasticity_coefficient) < 1:
            sensitivity = "inelastic"
        elif abs(elasticity_coefficient) < 1.5:
            sensitivity = "moderate"
        else:
            sensitivity = "elastic"

        return PriceElasticity(
            elasticity_coefficient=elasticity_coefficient,
            demand_sensitivity=sensitivity,
            optimal_price_range=optimal_range,
            confidence_score=0.8
        )

    async def _generate_revenue_projections(
        self,
        current_price: float,
        cost: float,
        elasticity: PriceElasticity,
        features: np.ndarray
    ) -> List[RevenueProjection]:
        """Generate revenue projections for different price points"""

        projections = []
        base_demand = 100  # Mock base demand

        # Test different price points
        price_points = np.linspace(current_price * 0.8, current_price * 1.2, 9)

        for price in price_points:
            price_change_percent = ((price - current_price) / current_price) * 100
            demand_change = elasticity.elasticity_coefficient * price_change_percent
            projected_demand = int(base_demand * (1 + demand_change / 100))

            projected_revenue = price * projected_demand
            projected_profit = (price - cost) * projected_demand

            confidence_interval = (
                projected_revenue * 0.9,
                projected_revenue * 1.1
            )

            projections.append(RevenueProjection(
                price_point=round(price, 2),
                projected_demand=max(0, projected_demand),
                projected_revenue=round(projected_revenue, 2),
                projected_profit=round(projected_profit, 2),
                confidence_interval=confidence_interval
            ))

        return projections

    async def _analyze_price_change_reasons(
        self,
        current_price: float,
        optimized_price: float,
        features: np.ndarray,
        competitor_prices: List[Dict[str, Any]]
    ) -> Tuple[PriceChangeReason, List[PriceChangeReason]]:
        """Analyze reasons for price change recommendation"""

        reasons = []
        price_change = optimized_price - current_price

        # Competitive pressure
        if competitor_prices:
            avg_competitor_price = np.mean([cp.get('price', 0) for cp in competitor_prices])
            if current_price > avg_competitor_price * 1.1:
                reasons.append(PriceChangeReason.COMPETITIVE_PRESSURE)

        # Price direction analysis
        if price_change > 0:
            reasons.extend([
                PriceChangeReason.DEMAND_INCREASE,
                PriceChangeReason.MARKET_POSITION
            ])
        elif price_change < 0:
            reasons.extend([
                PriceChangeReason.COMPETITIVE_PRESSURE,
                PriceChangeReason.DEMAND_DECREASE
            ])

        # Default to value-based if no specific reason
        if not reasons:
            reasons.append(PriceChangeReason.MARKET_POSITION)

        primary_reason = reasons[0] if reasons else PriceChangeReason.MARKET_POSITION
        secondary_reasons = reasons[1:] if len(reasons) > 1 else []

        return primary_reason, secondary_reasons

    async def _analyze_competition(
        self,
        optimized_price: float,
        competitor_prices: List[Dict[str, Any]]
    ) -> Dict[str, Any]:
        """Analyze competitive position"""

        if not competitor_prices:
            return {"position": "unknown", "analysis": "No competitor data available"}

        prices = [cp.get('price', 0) for cp in competitor_prices]
        avg_price = np.mean(prices)
        min_price = np.min(prices)
        max_price = np.max(prices)

        position = "competitive"
        if optimized_price > avg_price * 1.1:
            position = "premium"
        elif optimized_price < avg_price * 0.9:
            position = "value"

        price_rank = sum(1 for p in prices if optimized_price > p) + 1

        return {
            "position": position,
            "price_rank": price_rank,
            "price_percentile": (price_rank / (len(prices) + 1)) * 100,
            "price_gap_to_avg": ((optimized_price - avg_price) / avg_price) * 100,
            "closest_competitor_gap": min(abs(optimized_price - p) for p in prices),
            "market_spread": max_price - min_price,
            "competitive_intensity": "high" if len(prices) > 5 else "moderate"
        }

    async def _assess_risks(
        self,
        current_price: float,
        optimized_price: float,
        features: np.ndarray
    ) -> List[str]:
        """Assess risks associated with price change"""

        risks = []
        price_change_percent = abs((optimized_price - current_price) / current_price) * 100

        if price_change_percent > 10:
            risks.append("Large price change may shock customers")

        if optimized_price > current_price:
            risks.append("Price increase may reduce demand")
            risks.append("Competitors may maintain lower prices")
        else:
            risks.append("Price decrease may signal quality concerns")
            risks.append("Profit margins will be reduced")

        # Add more sophisticated risk analysis here

        return risks

    async def _calculate_confidence_score(
        self,
        predictions: List[ModelPrediction],
        features: np.ndarray,
        demand_data: Optional[Dict[str, Any]] = None
    ) -> float:
        """Calculate confidence score for optimization"""

        if not predictions:
            return 0.0

        # Base confidence from model ensemble
        avg_confidence = np.mean([p.confidence for p in predictions])

        # Adjust based on data quality
        data_quality_factor = 1.0

        # Reduce confidence if limited demand data
        if not demand_data or not demand_data.get('daily_sales'):
            data_quality_factor *= 0.8

        # Reduce confidence if predictions vary widely
        if len(predictions) > 1:
            price_std = np.std([p.price for p in predictions])
            avg_price = np.mean([p.price for p in predictions])
            if avg_price > 0:
                price_cv = price_std / avg_price
                if price_cv > 0.1:  # High variation
                    data_quality_factor *= 0.9

        confidence = min(1.0, avg_confidence * data_quality_factor)
        return round(confidence, 2)

    async def _determine_market_position(
        self,
        optimized_price: float,
        competitor_prices: List[Dict[str, Any]]
    ) -> str:
        """Determine recommended market position"""

        if not competitor_prices:
            return "value_leader"

        avg_competitor_price = np.mean([cp.get('price', 0) for cp in competitor_prices])

        if optimized_price > avg_competitor_price * 1.15:
            return "premium"
        elif optimized_price > avg_competitor_price * 1.05:
            return "competitive_premium"
        elif optimized_price > avg_competitor_price * 0.95:
            return "competitive"
        elif optimized_price > avg_competitor_price * 0.85:
            return "value"
        else:
            return "value_leader"

    async def _assess_data_quality(
        self,
        features: np.ndarray,
        demand_data: Optional[Dict[str, Any]] = None
    ) -> float:
        """Assess quality of input data"""

        quality_score = 1.0

        # Check for missing or invalid features
        if np.any(np.isnan(features)) or np.any(np.isinf(features)):
            quality_score *= 0.7

        # Check demand data quality
        if not demand_data:
            quality_score *= 0.8
        elif demand_data.get('daily_sales'):
            sales_data = demand_data['daily_sales']
            if len(sales_data) < 30:  # Less than 30 days of data
                quality_score *= 0.9

        return round(quality_score, 2)


# Helper function for trend calculation
def trend_coefficient(y):
    """Calculate trend coefficient for time series data"""
    if len(y) < 2:
        return 0
    x = np.arange(len(y))
    return np.polyfit(x, y, 1)[0]


# Add trend coefficient to numpy
np.trend_coefficient = trend_coefficient