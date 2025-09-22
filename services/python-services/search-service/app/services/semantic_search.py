"""
Semantic search service using NLP models
"""

import asyncio
from typing import List, Optional, Dict, Any
import structlog
import numpy as np
from sentence_transformers import SentenceTransformer
import spacy

from app.core.config import get_settings
from app.models.schemas import SearchResponse, ProductResult

settings = get_settings()
logger = structlog.get_logger(__name__)


class SemanticSearchService:
    """Semantic search using sentence transformers and NLP"""

    def __init__(self):
        self.sentence_model: Optional[SentenceTransformer] = None
        self.nlp_model: Optional[spacy.Language] = None
        self.product_embeddings: Dict[str, np.ndarray] = {}

    async def load_models(self):
        """Load NLP models"""
        try:
            # Load sentence transformer model
            self.sentence_model = SentenceTransformer(settings.SENTENCE_TRANSFORMER_MODEL)
            logger.info("Sentence transformer model loaded")

            # Load spaCy model
            self.nlp_model = spacy.load(settings.SPACY_MODEL)
            logger.info("spaCy model loaded")

            logger.info("Semantic search models loaded successfully")

        except Exception as e:
            logger.error("Failed to load semantic search models", error=str(e))
            raise

    async def semantic_search(
        self,
        query: str,
        search_type: str = "semantic",
        similarity_threshold: float = 0.7,
        max_results: int = 20,
        filters: Optional[Dict[str, Any]] = None
    ) -> SearchResponse:
        """Execute semantic search"""
        try:
            logger.info("Executing semantic search", query=query, search_type=search_type)

            if not self.sentence_model:
                raise RuntimeError("Sentence model not loaded")

            # Generate query embedding
            query_embedding = self.sentence_model.encode([query])[0]

            # Find similar products
            similar_products = await self._find_similar_products(
                query_embedding, similarity_threshold, max_results, filters
            )

            return SearchResponse(
                query=query,
                total_results=len(similar_products),
                products=similar_products,
                search_time_ms=120
            )

        except Exception as e:
            logger.error("Semantic search failed", error=str(e), query=query)
            return SearchResponse(
                query=query,
                total_results=0,
                products=[],
                search_time_ms=0
            )

    async def _find_similar_products(
        self,
        query_embedding: np.ndarray,
        similarity_threshold: float,
        max_results: int,
        filters: Optional[Dict[str, Any]]
    ) -> List[ProductResult]:
        """Find products similar to query embedding"""
        try:
            # Mock implementation - in real system, this would:
            # 1. Load product embeddings from vector database
            # 2. Calculate cosine similarity
            # 3. Apply filters
            # 4. Return top results

            # For now, return mock results
            mock_products = [
                ProductResult(
                    id=f"prod_{i}",
                    name=f"Semantic Product {i}",
                    description=f"Product found through semantic search {i}",
                    price=99.99 + i * 10,
                    relevance_score=0.9 - i * 0.1
                )
                for i in range(min(max_results, 5))
            ]

            return mock_products

        except Exception as e:
            logger.error("Failed to find similar products", error=str(e))
            return []

    async def extract_entities(self, text: str) -> Dict[str, List[str]]:
        """Extract named entities from text"""
        try:
            if not self.nlp_model:
                raise RuntimeError("NLP model not loaded")

            doc = self.nlp_model(text)
            entities = {}

            for ent in doc.ents:
                if ent.label_ not in entities:
                    entities[ent.label_] = []
                entities[ent.label_].append(ent.text)

            return entities

        except Exception as e:
            logger.error("Entity extraction failed", error=str(e))
            return {}

    async def expand_query(self, query: str) -> List[str]:
        """Expand query with synonyms and related terms"""
        try:
            if not self.nlp_model:
                raise RuntimeError("NLP model not loaded")

            doc = self.nlp_model(query)
            expanded_terms = [query]

            # Add synonyms and similar words
            for token in doc:
                if token.has_vector and not token.is_stop:
                    # In a real implementation, this would use word vectors
                    # to find similar terms
                    pass

            return expanded_terms

        except Exception as e:
            logger.error("Query expansion failed", error=str(e))
            return [query]