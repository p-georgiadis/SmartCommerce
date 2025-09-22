"""
Fraud Detection Service

This module contains the core fraud detection logic using advanced machine learning
algorithms, rule-based systems, and anomaly detection to identify fraudulent
transactions and suspicious activities in real-time.
"""

import asyncio
import json
import hashlib
import numpy as np
import pandas as pd
from datetime import datetime, timedelta
from typing import Dict, List, Optional, Tuple, Any
from dataclasses import dataclass
from decimal import Decimal

import joblib
from sklearn.ensemble import IsolationForest, RandomForestClassifier
from sklearn.preprocessing import StandardScaler, LabelEncoder
from sklearn.model_selection import cross_val_score
from xgboost import XGBClassifier
import lightgbm as lgb
from pyod.models.auto_encoder import AutoEncoder
from pyod.models.lof import LOF
from imblearn.over_sampling import SMOTE

import structlog
from app.core.config import get_settings
from app.models.schemas import (
    TransactionData,
    UserData,
    DeviceInfo,
    ContextData,
    TransactionAnalysisResponse,
    FraudDecision,
    RiskLevel,
    FraudIndicator,
    RiskFactor,
    FraudAlert,
    FraudType,
    FraudStatistics
)

logger = structlog.get_logger()
settings = get_settings()


@dataclass
class FraudDetectionResult:
    """Fraud detection result"""
    fraud_score: float
    decision: FraudDecision
    confidence_score: float
    indicators: List[FraudIndicator]
    recommendations: List[str]
    primary_indicator: Optional[str]
    processing_time_ms: float
    model_version: str
    analysis_timestamp: datetime


class FraudDetectionService:
    """Advanced fraud detection service using ensemble ML models"""

    def __init__(self):
        self.models: Dict[str, Any] = {}
        self.scalers: Dict[str, StandardScaler] = {}
        self.encoders: Dict[str, LabelEncoder] = {}
        self.is_initialized = False

        # Model configurations
        self.model_configs = {
            'random_forest': {
                'class': RandomForestClassifier,
                'params': {
                    'n_estimators': 100,
                    'max_depth': 10,
                    'min_samples_split': 5,
                    'random_state': 42,
                    'class_weight': 'balanced'
                }
            },
            'xgboost': {
                'class': XGBClassifier,
                'params': {
                    'n_estimators': 100,
                    'max_depth': 6,
                    'learning_rate': 0.1,
                    'random_state': 42,
                    'scale_pos_weight': 10  # Handle imbalanced data
                }
            },
            'lightgbm': {
                'class': lgb.LGBMClassifier,
                'params': {
                    'n_estimators': 100,
                    'max_depth': 6,
                    'learning_rate': 0.1,
                    'random_state': 42,
                    'class_weight': 'balanced'
                }
            },
            'isolation_forest': {
                'class': IsolationForest,
                'params': {
                    'contamination': 0.1,
                    'random_state': 42
                }
            },
            'autoencoder': {
                'class': AutoEncoder,
                'params': {
                    'contamination': 0.1,
                    'random_state': 42
                }
            },
            'local_outlier': {
                'class': LOF,
                'params': {
                    'contamination': 0.1
                }
            }
        }

        # Rule thresholds
        self.thresholds = {
            'high_amount': 10000,
            'velocity_transactions': 10,
            'velocity_timeframe_hours': 1,
            'international_risk_score': 0.7,
            'device_risk_threshold': 0.8,
            'behavioral_anomaly_threshold': 0.75
        }

    async def load_models(self):
        """Load and initialize fraud detection models"""
        try:
            # Initialize models with default configurations
            for model_name, config in self.model_configs.items():
                self.models[model_name] = config['class'](**config['params'])
                if model_name not in ['isolation_forest', 'autoencoder', 'local_outlier']:
                    self.scalers[model_name] = StandardScaler()

            # Initialize encoders for categorical features
            self.encoders = {
                'country': LabelEncoder(),
                'payment_type': LabelEncoder(),
                'device_type': LabelEncoder(),
                'merchant_category': LabelEncoder()
            }

            # Try to load pre-trained models if they exist
            try:
                await self._load_pretrained_models()
            except FileNotFoundError:
                logger.info("No pre-trained fraud models found, using default models")

            self.is_initialized = True
            logger.info("Fraud detection models loaded successfully")

        except Exception as e:
            logger.error("Failed to load fraud detection models", error=str(e))
            raise

    async def _load_pretrained_models(self):
        """Load pre-trained models from storage"""
        import os
        model_path = settings.MODEL_STORAGE_PATH

        if os.path.exists(model_path):
            for model_name in self.model_configs.keys():
                model_file = os.path.join(model_path, f"{model_name}_fraud_detector.joblib")
                if os.path.exists(model_file):
                    self.models[model_name] = joblib.load(model_file)
                    logger.info(f"Loaded pre-trained fraud model: {model_name}")

    async def analyze_transaction(
        self,
        transaction_data: TransactionData,
        user_data: UserData,
        device_data: DeviceInfo,
        context_data: ContextData
    ) -> FraudDetectionResult:
        """Analyze a transaction for fraud indicators"""

        start_time = asyncio.get_event_loop().time()

        logger.info("Starting fraud analysis", transaction_id=transaction_data.transaction_id)

        try:
            # Prepare features for ML models
            features = await self._prepare_transaction_features(
                transaction_data, user_data, device_data, context_data
            )

            # Rule-based analysis
            rule_indicators = await self._apply_fraud_rules(
                transaction_data, user_data, device_data, context_data
            )

            # ML-based analysis
            ml_scores = await self._run_ml_models(features)

            # Anomaly detection
            anomaly_scores = await self._detect_anomalies(features)

            # Device fingerprinting analysis
            device_risk = await self._analyze_device_risk(device_data, user_data)

            # Behavioral analysis
            behavioral_risk = await self._analyze_behavioral_patterns(
                transaction_data, user_data, context_data
            )

            # Velocity analysis
            velocity_risk = await self._analyze_velocity_patterns(
                transaction_data, user_data
            )

            # Geographic analysis
            geographic_risk = await self._analyze_geographic_risk(
                transaction_data, user_data, device_data
            )

            # Combine all scores
            fraud_score = await self._calculate_ensemble_score(
                ml_scores=ml_scores,
                anomaly_scores=anomaly_scores,
                rule_indicators=rule_indicators,
                device_risk=device_risk,
                behavioral_risk=behavioral_risk,
                velocity_risk=velocity_risk,
                geographic_risk=geographic_risk
            )

            # Determine decision
            decision = await self._make_fraud_decision(fraud_score, rule_indicators)

            # Generate indicators and recommendations
            indicators = await self._generate_fraud_indicators(
                rule_indicators, ml_scores, anomaly_scores, device_risk,
                behavioral_risk, velocity_risk, geographic_risk
            )

            recommendations = await self._generate_recommendations(
                fraud_score, decision, indicators
            )

            # Calculate confidence
            confidence_score = await self._calculate_confidence(
                ml_scores, anomaly_scores, features
            )

            processing_time = (asyncio.get_event_loop().time() - start_time) * 1000

            result = FraudDetectionResult(
                fraud_score=fraud_score,
                decision=decision,
                confidence_score=confidence_score,
                indicators=indicators,
                recommendations=recommendations,
                primary_indicator=indicators[0].indicator_type if indicators else None,
                processing_time_ms=processing_time,
                model_version="ensemble_v2.0",
                analysis_timestamp=datetime.utcnow()
            )

            logger.info(
                "Fraud analysis completed",
                transaction_id=transaction_data.transaction_id,
                fraud_score=fraud_score,
                decision=decision.value,
                processing_time_ms=processing_time
            )

            return result

        except Exception as e:
            logger.error(
                "Fraud analysis failed",
                transaction_id=transaction_data.transaction_id,
                error=str(e)
            )
            raise

    async def bulk_analyze_transactions(
        self,
        transactions: List[Dict[str, Any]],
        analysis_options: Optional[Dict[str, Any]] = None
    ) -> Dict[str, TransactionAnalysisResponse]:
        """Analyze multiple transactions for fraud"""

        logger.info("Starting bulk fraud analysis", transaction_count=len(transactions))

        results = {}

        # Process in batches to avoid overwhelming the system
        batch_size = 20
        for i in range(0, len(transactions), batch_size):
            batch = transactions[i:i + batch_size]

            # Process batch concurrently
            batch_tasks = []
            for transaction_request in batch:
                task = self.analyze_transaction(
                    transaction_data=transaction_request['transaction'],
                    user_data=transaction_request['user'],
                    device_data=transaction_request['device'],
                    context_data=transaction_request['context']
                )
                batch_tasks.append(task)

            # Execute batch
            batch_results = await asyncio.gather(*batch_tasks, return_exceptions=True)

            # Process results
            for j, result in enumerate(batch_results):
                transaction_id = batch[j]['transaction'].transaction_id
                if isinstance(result, Exception):
                    logger.error("Failed to analyze transaction",
                               transaction_id=transaction_id, error=str(result))
                else:
                    # Convert to response format
                    response = TransactionAnalysisResponse(
                        transaction_id=transaction_id,
                        fraud_score=result.fraud_score,
                        risk_level=self._score_to_risk_level(result.fraud_score),
                        decision=result.decision,
                        indicators=result.indicators,
                        risk_factors=[],  # Would be populated in real implementation
                        recommendations=result.recommendations,
                        confidence_score=result.confidence_score,
                        processing_time_ms=result.processing_time_ms,
                        model_version=result.model_version,
                        analysis_timestamp=result.analysis_timestamp
                    )
                    results[transaction_id] = response

        logger.info("Bulk fraud analysis completed",
                   total_transactions=len(transactions),
                   successful=len(results))

        return results

    async def _prepare_transaction_features(
        self,
        transaction: TransactionData,
        user: UserData,
        device: DeviceInfo,
        context: ContextData
    ) -> np.ndarray:
        """Prepare feature vector for ML models"""

        features = []

        # Transaction features
        features.extend([
            float(transaction.amount),
            np.log1p(float(transaction.amount)),  # log amount
            transaction.transaction_time.hour,
            transaction.transaction_time.weekday(),
            1 if transaction.is_international else 0,
            1 if transaction.is_recurring else 0,
            transaction.hourly_transaction_count or 0,
            transaction.daily_transaction_count or 0,
            float(transaction.daily_amount_total or 0),
        ])

        # User features
        features.extend([
            user.account_age_days,
            np.log1p(user.account_age_days),
            1 if user.email_verified else 0,
            1 if user.phone_verified else 0,
            user.previous_fraud_reports,
            user.chargebacks_count,
            user.failed_login_attempts,
            float(user.average_transaction_amount or 0),
            user.transaction_frequency or 0,
            user.profile_completeness or 0,
        ])

        # Device features
        features.extend([
            1 if device.is_mobile else 0,
            1 if device.is_proxy else 0,
            len(device.device_fingerprint or ""),
            self._encode_categorical(device.device_type, 'device_type'),
        ])

        # Payment features
        payment = transaction.payment_method
        features.extend([
            self._encode_categorical(payment.payment_type, 'payment_type'),
            1 if payment.is_tokenized else 0,
            payment.token_confidence or 0,
        ])

        # Geographic features
        if transaction.billing_address and transaction.shipping_address:
            billing_country = transaction.billing_address.get('country', 'unknown')
            shipping_country = transaction.shipping_address.get('country', 'unknown')
            features.extend([
                self._encode_categorical(billing_country, 'country'),
                self._encode_categorical(shipping_country, 'country'),
                1 if billing_country != shipping_country else 0,
            ])
        else:
            features.extend([0, 0, 0])

        # Time-based features
        if user.last_login_time:
            time_since_login = (transaction.transaction_time - user.last_login_time).total_seconds()
            features.append(np.log1p(time_since_login))
        else:
            features.append(0)

        # Session features
        features.extend([
            context.session_duration or 0,
            context.pages_visited or 0,
            context.time_to_transaction or 0,
            1 if context.holiday_indicator else 0,
            1 if context.promotional_period else 0,
        ])

        return np.array(features).reshape(1, -1)

    def _encode_categorical(self, value: str, encoder_name: str) -> float:
        """Encode categorical value"""
        try:
            if encoder_name in self.encoders:
                # In a real implementation, encoders would be fitted on training data
                return hash(value) % 100  # Simple hash encoding for demo
            return 0
        except:
            return 0

    async def _apply_fraud_rules(
        self,
        transaction: TransactionData,
        user: UserData,
        device: DeviceInfo,
        context: ContextData
    ) -> List[Dict[str, Any]]:
        """Apply rule-based fraud detection"""

        indicators = []

        # High amount rule
        if float(transaction.amount) > self.thresholds['high_amount']:
            indicators.append({
                'type': 'high_amount',
                'severity': min(float(transaction.amount) / 50000, 1.0),
                'description': f"High transaction amount: ${transaction.amount}"
            })

        # Velocity rules
        if transaction.hourly_transaction_count and \
           transaction.hourly_transaction_count > self.thresholds['velocity_transactions']:
            indicators.append({
                'type': 'velocity_abuse',
                'severity': min(transaction.hourly_transaction_count / 20, 1.0),
                'description': f"High transaction velocity: {transaction.hourly_transaction_count} in 1 hour"
            })

        # New account rule
        if user.account_age_days < 1:
            indicators.append({
                'type': 'new_account',
                'severity': 0.7,
                'description': "Transaction from newly created account"
            })

        # Unverified account rule
        if not user.email_verified or not user.phone_verified:
            indicators.append({
                'type': 'unverified_account',
                'severity': 0.5,
                'description': "Transaction from unverified account"
            })

        # Device risk rules
        if device.is_proxy:
            indicators.append({
                'type': 'proxy_usage',
                'severity': 0.6,
                'description': "Transaction from proxy/VPN"
            })

        # Time-based rules
        hour = transaction.transaction_time.hour
        if hour < 6 or hour > 23:  # Late night transactions
            indicators.append({
                'type': 'unusual_time',
                'severity': 0.4,
                'description': "Transaction during unusual hours"
            })

        # Previous fraud history
        if user.previous_fraud_reports > 0:
            indicators.append({
                'type': 'fraud_history',
                'severity': min(user.previous_fraud_reports / 5, 1.0),
                'description': f"User has {user.previous_fraud_reports} previous fraud reports"
            })

        # Failed login attempts
        if user.failed_login_attempts > 3:
            indicators.append({
                'type': 'failed_logins',
                'severity': min(user.failed_login_attempts / 10, 1.0),
                'description': f"Recent failed login attempts: {user.failed_login_attempts}"
            })

        return indicators

    async def _run_ml_models(self, features: np.ndarray) -> Dict[str, float]:
        """Run ML models and get fraud scores"""

        scores = {}

        # Supervised models (would need to be trained on labeled data)
        supervised_models = ['random_forest', 'xgboost', 'lightgbm']

        for model_name in supervised_models:
            try:
                model = self.models[model_name]

                # Scale features if scaler exists
                if model_name in self.scalers:
                    scaled_features = self.scalers[model_name].fit_transform(features)
                else:
                    scaled_features = features

                # Get prediction probability for fraud class
                # Note: In real implementation, models would be trained on fraud data
                fraud_prob = np.random.beta(2, 8)  # Mock fraud probability
                scores[model_name] = fraud_prob

            except Exception as e:
                logger.warning(f"ML model {model_name} failed", error=str(e))
                scores[model_name] = 0.0

        return scores

    async def _detect_anomalies(self, features: np.ndarray) -> Dict[str, float]:
        """Detect anomalies using unsupervised models"""

        scores = {}

        # Anomaly detection models
        anomaly_models = ['isolation_forest', 'autoencoder', 'local_outlier']

        for model_name in anomaly_models:
            try:
                model = self.models[model_name]

                # Get anomaly score
                # Note: In real implementation, models would be fitted on normal transaction data
                if hasattr(model, 'decision_function'):
                    anomaly_score = abs(model.decision_function(features)[0])
                else:
                    anomaly_score = np.random.beta(2, 8)  # Mock anomaly score

                # Normalize to 0-1 range
                scores[model_name] = min(anomaly_score, 1.0)

            except Exception as e:
                logger.warning(f"Anomaly model {model_name} failed", error=str(e))
                scores[model_name] = 0.0

        return scores

    async def _analyze_device_risk(self, device: DeviceInfo, user: UserData) -> float:
        """Analyze device-based risk factors"""

        risk_score = 0.0
        factors = []

        # Device fingerprint analysis
        if not device.device_fingerprint:
            risk_score += 0.3
            factors.append("missing_fingerprint")

        # Proxy/VPN usage
        if device.is_proxy:
            risk_score += 0.4
            factors.append("proxy_usage")

        # Geolocation consistency
        if device.geolocation:
            # Check if device location matches user's typical location
            # This would use historical location data in real implementation
            pass

        # Device reputation
        device_hash = hashlib.md5(device.device_fingerprint.encode() if device.device_fingerprint else b"").hexdigest()
        # In real implementation, check device reputation database
        if hash(device_hash) % 100 > 95:  # Mock high-risk device
            risk_score += 0.5
            factors.append("high_risk_device")

        return min(risk_score, 1.0)

    async def _analyze_behavioral_patterns(
        self,
        transaction: TransactionData,
        user: UserData,
        context: ContextData
    ) -> float:
        """Analyze behavioral patterns for anomalies"""

        risk_score = 0.0

        # Time to transaction analysis
        if context.time_to_transaction:
            if context.time_to_transaction < 10:  # Very quick transaction
                risk_score += 0.3
            elif context.time_to_transaction > 3600:  # Very long session
                risk_score += 0.2

        # Session behavior
        if context.pages_visited:
            if context.pages_visited < 2:  # Direct to checkout
                risk_score += 0.2
            elif context.pages_visited > 50:  # Excessive browsing
                risk_score += 0.1

        # Typing patterns (if available)
        if context.typing_patterns:
            # Analyze typing speed, rhythm, etc.
            # This would use behavioral biometrics in real implementation
            pass

        # Purchase pattern analysis
        if user.average_transaction_amount:
            amount_ratio = float(transaction.amount) / float(user.average_transaction_amount)
            if amount_ratio > 5:  # Much larger than usual
                risk_score += 0.4
            elif amount_ratio < 0.1:  # Much smaller than usual
                risk_score += 0.2

        return min(risk_score, 1.0)

    async def _analyze_velocity_patterns(
        self,
        transaction: TransactionData,
        user: UserData
    ) -> float:
        """Analyze transaction velocity patterns"""

        risk_score = 0.0

        # Hourly velocity
        if transaction.hourly_transaction_count:
            if transaction.hourly_transaction_count > 5:
                risk_score += min(transaction.hourly_transaction_count / 10, 0.5)

        # Daily velocity
        if transaction.daily_transaction_count:
            if transaction.daily_transaction_count > 10:
                risk_score += min(transaction.daily_transaction_count / 20, 0.3)

        # Amount velocity
        if transaction.daily_amount_total and user.average_transaction_amount:
            daily_ratio = float(transaction.daily_amount_total) / (float(user.average_transaction_amount) * user.transaction_frequency or 1)
            if daily_ratio > 3:
                risk_score += min(daily_ratio / 10, 0.4)

        return min(risk_score, 1.0)

    async def _analyze_geographic_risk(
        self,
        transaction: TransactionData,
        user: UserData,
        device: DeviceInfo
    ) -> float:
        """Analyze geographic risk factors"""

        risk_score = 0.0

        # International transaction risk
        if transaction.is_international:
            risk_score += 0.3

        # Billing vs shipping address mismatch
        if transaction.billing_address and transaction.shipping_address:
            billing_country = transaction.billing_address.get('country')
            shipping_country = transaction.shipping_address.get('country')

            if billing_country != shipping_country:
                risk_score += 0.2

        # High-risk countries (would be configurable in real implementation)
        high_risk_countries = ['XX', 'YY', 'ZZ']  # Mock country codes

        if transaction.billing_address:
            billing_country = transaction.billing_address.get('country')
            if billing_country in high_risk_countries:
                risk_score += 0.4

        # Device location vs user location
        if device.geolocation and user.country:
            # Check if device location matches user's country
            # This would use geolocation services in real implementation
            pass

        return min(risk_score, 1.0)

    async def _calculate_ensemble_score(
        self,
        ml_scores: Dict[str, float],
        anomaly_scores: Dict[str, float],
        rule_indicators: List[Dict[str, Any]],
        device_risk: float,
        behavioral_risk: float,
        velocity_risk: float,
        geographic_risk: float
    ) -> float:
        """Calculate ensemble fraud score"""

        # Weighted combination of different score types
        weights = {
            'ml_models': 0.3,
            'anomaly_detection': 0.2,
            'rules': 0.2,
            'device_risk': 0.1,
            'behavioral_risk': 0.1,
            'velocity_risk': 0.05,
            'geographic_risk': 0.05
        }

        final_score = 0.0

        # ML model scores
        if ml_scores:
            avg_ml_score = np.mean(list(ml_scores.values()))
            final_score += avg_ml_score * weights['ml_models']

        # Anomaly scores
        if anomaly_scores:
            avg_anomaly_score = np.mean(list(anomaly_scores.values()))
            final_score += avg_anomaly_score * weights['anomaly_detection']

        # Rule-based scores
        if rule_indicators:
            avg_rule_score = np.mean([indicator['severity'] for indicator in rule_indicators])
            final_score += avg_rule_score * weights['rules']

        # Individual risk scores
        final_score += device_risk * weights['device_risk']
        final_score += behavioral_risk * weights['behavioral_risk']
        final_score += velocity_risk * weights['velocity_risk']
        final_score += geographic_risk * weights['geographic_risk']

        return min(final_score, 1.0)

    async def _make_fraud_decision(
        self,
        fraud_score: float,
        rule_indicators: List[Dict[str, Any]]
    ) -> FraudDecision:
        """Make fraud decision based on score and rules"""

        # Critical rules that force decline
        critical_indicators = [
            indicator for indicator in rule_indicators
            if indicator['severity'] > 0.9
        ]

        if critical_indicators:
            return FraudDecision.DECLINE

        # Score-based decisions
        if fraud_score >= 0.8:
            return FraudDecision.DECLINE
        elif fraud_score >= 0.6:
            return FraudDecision.REVIEW
        elif fraud_score >= 0.4:
            return FraudDecision.CHALLENGE
        else:
            return FraudDecision.APPROVE

    async def _generate_fraud_indicators(
        self,
        rule_indicators: List[Dict[str, Any]],
        ml_scores: Dict[str, float],
        anomaly_scores: Dict[str, float],
        device_risk: float,
        behavioral_risk: float,
        velocity_risk: float,
        geographic_risk: float
    ) -> List[FraudIndicator]:
        """Generate fraud indicators from analysis results"""

        indicators = []

        # Rule-based indicators
        for rule_indicator in rule_indicators:
            indicators.append(FraudIndicator(
                indicator_type=rule_indicator['type'],
                description=rule_indicator['description'],
                severity=rule_indicator['severity'],
                confidence=0.9,  # High confidence in rules
                contributing_factors=[rule_indicator['type']]
            ))

        # ML model indicators
        for model_name, score in ml_scores.items():
            if score > 0.5:
                indicators.append(FraudIndicator(
                    indicator_type=f"ml_{model_name}",
                    description=f"Machine learning model {model_name} detected fraud patterns",
                    severity=score,
                    confidence=0.7,
                    contributing_factors=[f"ml_prediction_{model_name}"]
                ))

        # Anomaly indicators
        for model_name, score in anomaly_scores.items():
            if score > 0.6:
                indicators.append(FraudIndicator(
                    indicator_type=f"anomaly_{model_name}",
                    description=f"Anomaly detection model {model_name} detected unusual patterns",
                    severity=score,
                    confidence=0.6,
                    contributing_factors=[f"anomaly_{model_name}"]
                ))

        # Risk-based indicators
        if device_risk > 0.5:
            indicators.append(FraudIndicator(
                indicator_type="device_risk",
                description="High device risk detected",
                severity=device_risk,
                confidence=0.8,
                contributing_factors=["device_analysis"]
            ))

        if behavioral_risk > 0.5:
            indicators.append(FraudIndicator(
                indicator_type="behavioral_risk",
                description="Unusual behavioral patterns detected",
                severity=behavioral_risk,
                confidence=0.7,
                contributing_factors=["behavioral_analysis"]
            ))

        # Sort by severity
        indicators.sort(key=lambda x: x.severity, reverse=True)

        return indicators

    async def _generate_recommendations(
        self,
        fraud_score: float,
        decision: FraudDecision,
        indicators: List[FraudIndicator]
    ) -> List[str]:
        """Generate recommendations based on analysis"""

        recommendations = []

        if decision == FraudDecision.DECLINE:
            recommendations.extend([
                "Block transaction immediately",
                "Alert fraud investigation team",
                "Consider temporary account suspension",
                "Require additional verification for future transactions"
            ])
        elif decision == FraudDecision.REVIEW:
            recommendations.extend([
                "Place transaction in manual review queue",
                "Contact customer for verification",
                "Monitor account for additional suspicious activity",
                "Consider lowering transaction limits temporarily"
            ])
        elif decision == FraudDecision.CHALLENGE:
            recommendations.extend([
                "Require additional authentication (2FA, SMS)",
                "Ask for additional verification documents",
                "Monitor transaction completion",
                "Set up enhanced monitoring for this account"
            ])

        # Specific recommendations based on indicators
        indicator_types = [ind.indicator_type for ind in indicators]

        if 'high_amount' in indicator_types:
            recommendations.append("Consider implementing daily/monthly spending limits")

        if 'velocity_abuse' in indicator_types:
            recommendations.append("Implement velocity controls and cooling-off periods")

        if 'proxy_usage' in indicator_types:
            recommendations.append("Block transactions from known proxy/VPN services")

        if 'new_account' in indicator_types:
            recommendations.append("Require additional verification for new accounts")

        return list(set(recommendations))  # Remove duplicates

    async def _calculate_confidence(
        self,
        ml_scores: Dict[str, float],
        anomaly_scores: Dict[str, float],
        features: np.ndarray
    ) -> float:
        """Calculate confidence in fraud analysis"""

        confidence_factors = []

        # Model agreement
        all_scores = list(ml_scores.values()) + list(anomaly_scores.values())
        if all_scores:
            score_std = np.std(all_scores)
            # High agreement = high confidence
            agreement_confidence = max(0, 1 - score_std * 2)
            confidence_factors.append(agreement_confidence)

        # Data quality
        # Check for missing features
        missing_ratio = np.isnan(features).sum() / features.size
        data_quality_confidence = 1 - missing_ratio
        confidence_factors.append(data_quality_confidence)

        # Base confidence
        confidence_factors.append(0.8)  # Base confidence level

        return np.mean(confidence_factors)

    def _score_to_risk_level(self, score: float) -> RiskLevel:
        """Convert fraud score to risk level"""
        if score >= 0.8:
            return RiskLevel.CRITICAL
        elif score >= 0.6:
            return RiskLevel.HIGH
        elif score >= 0.3:
            return RiskLevel.MEDIUM
        else:
            return RiskLevel.LOW

    async def get_recent_alerts(
        self,
        limit: int = 50,
        severity_filter: Optional[str] = None
    ) -> List[FraudAlert]:
        """Get recent fraud alerts"""
        # Mock implementation - would query from database in real system
        alerts = []

        for i in range(min(limit, 10)):  # Mock data
            alert = FraudAlert(
                alert_id=f"alert_{i}",
                transaction_id=f"txn_{i}",
                user_id=f"user_{i}",
                alert_type="high_fraud_score",
                fraud_type=FraudType.CARD_NOT_PRESENT,
                severity="high" if i % 3 == 0 else "medium",
                fraud_score=0.8 + (i % 3) * 0.05,
                description=f"High fraud score detected: {0.8 + (i % 3) * 0.05}",
                requires_immediate_action=i % 3 == 0,
                suggested_actions=["Review transaction", "Contact customer"],
                detection_model="ensemble_v2.0",
                confidence_level=0.85,
                timestamp=datetime.utcnow() - timedelta(hours=i),
                investigation_priority="high" if i % 3 == 0 else "medium"
            )
            alerts.append(alert)

        return alerts

    async def handle_false_positive(
        self,
        transaction_id: str,
        feedback: Optional[str] = None
    ):
        """Handle false positive feedback"""
        logger.info(
            "Processing false positive feedback",
            transaction_id=transaction_id,
            feedback=feedback
        )

        # In real implementation:
        # 1. Update model training data
        # 2. Adjust fraud rules
        # 3. Update user/transaction reputation
        # 4. Trigger model retraining if needed

    async def get_fraud_statistics(self, timeframe: str = "24h") -> FraudStatistics:
        """Get fraud detection statistics"""

        # Mock statistics - would query from database in real system
        stats = FraudStatistics(
            timeframe=timeframe,
            total_transactions_analyzed=10000,
            fraud_cases_detected=250,
            fraud_rate=2.5,
            true_positives=200,
            false_positives=50,
            false_negatives=30,
            precision=0.8,
            recall=0.87,
            f1_score=0.83,
            fraud_amount_detected=Decimal("125000.00"),
            fraud_amount_prevented=Decimal("100000.00"),
            false_positive_cost=Decimal("5000.00"),
            fraud_trends={"trend": "decreasing", "rate": -0.05},
            top_fraud_types=[
                {"type": "card_not_present", "count": 150},
                {"type": "account_takeover", "count": 75},
                {"type": "friendly_fraud", "count": 25}
            ],
            average_processing_time=0.15,
            system_availability=99.9,
            generated_at=datetime.utcnow()
        )

        return stats