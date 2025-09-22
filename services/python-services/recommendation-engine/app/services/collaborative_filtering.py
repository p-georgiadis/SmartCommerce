import numpy as np
import pandas as pd
from scipy.sparse import csr_matrix
from sklearn.metrics.pairwise import cosine_similarity
from sklearn.decomposition import TruncatedSVD
import implicit
from typing import List, Dict, Any, Optional, Tuple
import logging
import asyncio
from datetime import datetime, timedelta

logger = logging.getLogger(__name__)

class CollaborativeFilteringEngine:
    """
    Collaborative Filtering recommendation engine using Alternating Least Squares (ALS)
    and various similarity metrics for generating user-based and item-based recommendations.
    """

    def __init__(self, factors: int = 50, regularization: float = 0.01, iterations: int = 30):
        """
        Initialize the collaborative filtering engine

        Args:
            factors: Number of latent factors for matrix factorization
            regularization: Regularization parameter for ALS
            iterations: Number of iterations for ALS training
        """
        self.factors = factors
        self.regularization = regularization
        self.iterations = iterations

        # Initialize ALS model
        self.model = implicit.als.AlternatingLeastSquares(
            factors=factors,
            regularization=regularization,
            iterations=iterations,
            use_gpu=False,
            random_state=42
        )

        # Data structures
        self.user_item_matrix = None
        self.item_user_matrix = None
        self.user_mapper = {}
        self.item_mapper = {}
        self.reverse_user_mapper = {}
        self.reverse_item_mapper = {}

        # Model metadata
        self.is_trained = False
        self.last_training_time = None
        self.training_data_size = 0

        # Performance caching
        self._user_factors = None
        self._item_factors = None

    async def train(self, interactions_df: pd.DataFrame) -> 'CollaborativeFilteringEngine':
        """
        Train the collaborative filtering model on user-item interactions

        Args:
            interactions_df: DataFrame with columns ['user_id', 'item_id', 'rating']

        Returns:
            self: The trained engine instance
        """
        try:
            logger.info(f"Starting collaborative filtering training with {len(interactions_df)} interactions")

            # Validate input data
            required_columns = ['user_id', 'item_id', 'rating']
            missing_columns = [col for col in required_columns if col not in interactions_df.columns]
            if missing_columns:
                raise ValueError(f"Missing required columns: {missing_columns}")

            # Create user and item mappings
            unique_users = interactions_df['user_id'].unique()
            unique_items = interactions_df['item_id'].unique()

            self.user_mapper = {user: idx for idx, user in enumerate(unique_users)}
            self.item_mapper = {item: idx for idx, item in enumerate(unique_items)}
            self.reverse_user_mapper = {idx: user for user, idx in self.user_mapper.items()}
            self.reverse_item_mapper = {idx: item for item, idx in self.item_mapper.items()}

            # Create sparse user-item matrix
            self.user_item_matrix = await self._create_sparse_matrix(interactions_df)
            self.item_user_matrix = self.user_item_matrix.T.tocsr()

            # Train ALS model
            logger.info("Training ALS model...")
            await asyncio.get_event_loop().run_in_executor(
                None,
                self.model.fit,
                self.user_item_matrix
            )

            # Cache factor matrices for faster inference
            self._user_factors = self.model.user_factors
            self._item_factors = self.model.item_factors

            # Update metadata
            self.is_trained = True
            self.last_training_time = datetime.utcnow()
            self.training_data_size = len(interactions_df)

            logger.info(f"Collaborative filtering training completed. "
                       f"Users: {len(unique_users)}, Items: {len(unique_items)}")

            return self

        except Exception as e:
            logger.error(f"Error during collaborative filtering training: {str(e)}")
            raise

    async def get_recommendations(
        self,
        user_id: str,
        count: int = 10,
        filter_seen: bool = True,
        category: Optional[str] = None
    ) -> List[Dict[str, Any]]:
        """
        Generate recommendations for a specific user

        Args:
            user_id: The user to generate recommendations for
            count: Number of recommendations to return
            filter_seen: Whether to filter out already seen items
            category: Optional category filter

        Returns:
            List of recommendation dictionaries with product_id, score, and metadata
        """
        if not self.is_trained:
            logger.warning("Model not trained, returning popular items")
            return await self._get_popular_items(count)

        user_idx = self.user_mapper.get(user_id)
        if user_idx is None:
            logger.info(f"User {user_id} not found in training data, returning popular items")
            return await self._get_popular_items(count)

        try:
            # Get recommendations from ALS model
            recommended_items, scores = await asyncio.get_event_loop().run_in_executor(
                None,
                self._get_als_recommendations,
                user_idx,
                count * 2,  # Get more to allow for filtering
                filter_seen
            )

            # Convert to product IDs and prepare response
            recommendations = []
            for item_idx, score in zip(recommended_items, scores):
                if len(recommendations) >= count:
                    break

                product_id = self.reverse_item_mapper.get(item_idx)
                if product_id:
                    recommendations.append({
                        "product_id": product_id,
                        "score": float(score),
                        "reason": "Based on users with similar preferences",
                        "algorithm": "collaborative_filtering",
                        "confidence": self._calculate_confidence(score, user_idx)
                    })

            logger.info(f"Generated {len(recommendations)} collaborative filtering recommendations for user {user_id}")
            return recommendations

        except Exception as e:
            logger.error(f"Error generating recommendations for user {user_id}: {str(e)}")
            return await self._get_popular_items(count)

    async def get_similar_users(self, user_id: str, count: int = 10) -> List[Dict[str, Any]]:
        """
        Find users similar to the given user

        Args:
            user_id: The user to find similar users for
            count: Number of similar users to return

        Returns:
            List of similar user dictionaries with user_id and similarity score
        """
        if not self.is_trained:
            return []

        user_idx = self.user_mapper.get(user_id)
        if user_idx is None:
            return []

        try:
            # Calculate user similarities using cosine similarity
            user_vector = self.user_item_matrix[user_idx].toarray().flatten()
            similarities = await asyncio.get_event_loop().run_in_executor(
                None,
                self._calculate_user_similarities,
                user_vector
            )

            # Get top similar users (excluding the user themselves)
            similar_user_indices = np.argsort(similarities)[::-1][1:count+1]

            similar_users = []
            for idx in similar_user_indices:
                similar_user_id = self.reverse_user_mapper.get(idx)
                if similar_user_id and similarities[idx] > 0:
                    similar_users.append({
                        "user_id": similar_user_id,
                        "similarity_score": float(similarities[idx]),
                        "algorithm": "collaborative_filtering"
                    })

            return similar_users

        except Exception as e:
            logger.error(f"Error finding similar users for {user_id}: {str(e)}")
            return []

    async def get_item_similarities(self, item_id: str, count: int = 10) -> List[Dict[str, Any]]:
        """
        Find items similar to the given item using collaborative filtering

        Args:
            item_id: The item to find similar items for
            count: Number of similar items to return

        Returns:
            List of similar item dictionaries with product_id and similarity score
        """
        if not self.is_trained:
            return []

        item_idx = self.item_mapper.get(item_id)
        if item_idx is None:
            return []

        try:
            # Use implicit's similar_items method
            similar_items, scores = await asyncio.get_event_loop().run_in_executor(
                None,
                self.model.similar_items,
                item_idx,
                count + 1  # +1 to exclude the item itself
            )

            # Convert to product IDs and prepare response
            similar_products = []
            for sim_item_idx, score in zip(similar_items[1:], scores[1:]):  # Skip first (self)
                product_id = self.reverse_item_mapper.get(sim_item_idx)
                if product_id:
                    similar_products.append({
                        "product_id": product_id,
                        "score": float(score),
                        "reason": "Users who liked this also liked",
                        "algorithm": "collaborative_filtering"
                    })

            return similar_products

        except Exception as e:
            logger.error(f"Error finding similar items for {item_id}: {str(e)}")
            return []

    async def incremental_update(self, new_interactions: pd.DataFrame):
        """
        Perform incremental model update with new interaction data

        Args:
            new_interactions: New interaction data to incorporate
        """
        try:
            logger.info(f"Performing incremental update with {len(new_interactions)} new interactions")

            # For now, we'll do a simple retrain
            # In a production system, you might implement true incremental learning
            # or batch updates at regular intervals

            # This is a simplified approach - in reality, you'd want to:
            # 1. Merge new interactions with existing data
            # 2. Update user/item mappings for new entities
            # 3. Incrementally update the model

            await self.train(new_interactions)

        except Exception as e:
            logger.error(f"Error during incremental update: {str(e)}")
            raise

    def get_model_info(self) -> Dict[str, Any]:
        """Get information about the current model state"""
        return {
            "is_trained": self.is_trained,
            "last_training_time": self.last_training_time.isoformat() if self.last_training_time else None,
            "training_data_size": self.training_data_size,
            "num_users": len(self.user_mapper),
            "num_items": len(self.item_mapper),
            "factors": self.factors,
            "regularization": self.regularization,
            "iterations": self.iterations,
            "algorithm": "alternating_least_squares"
        }

    async def _create_sparse_matrix(self, interactions_df: pd.DataFrame) -> csr_matrix:
        """Create sparse user-item matrix from interactions DataFrame"""
        def create_matrix():
            # Map user and item IDs to indices
            user_indices = interactions_df['user_id'].map(self.user_mapper)
            item_indices = interactions_df['item_id'].map(self.item_mapper)

            # Create sparse matrix
            return csr_matrix(
                (interactions_df['rating'], (user_indices, item_indices)),
                shape=(len(self.user_mapper), len(self.item_mapper))
            )

        return await asyncio.get_event_loop().run_in_executor(None, create_matrix)

    def _get_als_recommendations(self, user_idx: int, count: int, filter_seen: bool) -> Tuple[np.ndarray, np.ndarray]:
        """Get recommendations from ALS model (blocking operation)"""
        user_items = self.user_item_matrix[user_idx] if filter_seen else None
        return self.model.recommend(
            user_idx,
            user_items,
            N=count,
            filter_already_liked_items=filter_seen
        )

    def _calculate_user_similarities(self, user_vector: np.ndarray) -> np.ndarray:
        """Calculate similarities between user and all other users"""
        user_matrix = self.user_item_matrix.toarray()
        similarities = cosine_similarity([user_vector], user_matrix)[0]
        return similarities

    def _calculate_confidence(self, score: float, user_idx: int) -> float:
        """Calculate confidence score based on user's interaction history"""
        user_interactions = self.user_item_matrix[user_idx].nnz
        # More interactions = higher confidence in recommendations
        confidence_boost = min(user_interactions / 50.0, 1.0)  # Cap at 1.0
        return min(score * (1 + confidence_boost), 1.0)

    async def _get_popular_items(self, count: int) -> List[Dict[str, Any]]:
        """Get popular items as fallback recommendations"""
        # In a real implementation, this would query popular items from a database
        # For now, return mock popular items
        popular_items = []
        for i in range(min(count, 10)):
            popular_items.append({
                "product_id": f"popular_item_{i+1}",
                "score": 0.9 - (i * 0.05),
                "reason": "Popular item among all users",
                "algorithm": "popularity_fallback",
                "confidence": 0.7
            })

        return popular_items