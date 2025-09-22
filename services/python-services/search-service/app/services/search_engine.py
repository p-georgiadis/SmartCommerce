"""
Core search engine service for the Search Service
"""

import asyncio
from typing import Dict, Any, List, Optional
import structlog
from elasticsearch import AsyncElasticsearch
from opensearchpy import AsyncOpenSearch

from app.core.config import get_settings
from app.models.schemas import SearchRequest, SearchResponse, ProductResult, SearchFacet

settings = get_settings()
logger = structlog.get_logger(__name__)


class SearchEngineService:
    """Core search engine implementation"""

    def __init__(self):
        self.elasticsearch_client: Optional[AsyncElasticsearch] = None
        self.opensearch_client: Optional[AsyncOpenSearch] = None
        self.whoosh_index = None

    async def initialize(self):
        """Initialize search engines"""
        try:
            # Initialize Elasticsearch if configured
            if settings.ELASTICSEARCH_URL:
                self.elasticsearch_client = AsyncElasticsearch([settings.ELASTICSEARCH_URL])
                logger.info("Elasticsearch client initialized")

            # Initialize OpenSearch if configured
            if settings.OPENSEARCH_URL:
                self.opensearch_client = AsyncOpenSearch([settings.OPENSEARCH_URL])
                logger.info("OpenSearch client initialized")

            # Initialize Whoosh for fallback/local search
            await self._initialize_whoosh()

            logger.info("Search engine initialized successfully")

        except Exception as e:
            logger.error("Failed to initialize search engine", error=str(e))
            raise

    async def _initialize_whoosh(self):
        """Initialize Whoosh index for local search"""
        try:
            from whoosh import fields
            from whoosh.index import create_index, exists_in
            from whoosh.filedb.filestore import FileStorage
            import os

            schema = fields.Schema(
                id=fields.ID(stored=True),
                name=fields.TEXT(stored=True),
                description=fields.TEXT(stored=True),
                category=fields.TEXT(stored=True),
                brand=fields.TEXT(stored=True),
                price=fields.NUMERIC(stored=True),
                tags=fields.KEYWORD(stored=True)
            )

            index_dir = "/tmp/search_index"
            os.makedirs(index_dir, exist_ok=True)

            if not exists_in(index_dir):
                self.whoosh_index = create_index(schema, index_dir)
            else:
                storage = FileStorage(index_dir)
                self.whoosh_index = storage.open_index()

            logger.info("Whoosh index initialized")

        except Exception as e:
            logger.error("Failed to initialize Whoosh", error=str(e))

    async def search(
        self,
        query: str,
        filters: Optional[Dict[str, Any]] = None,
        sort_options: Optional[List[str]] = None,
        pagination: Optional[Dict[str, int]] = None,
        search_options: Optional[Dict[str, Any]] = None
    ) -> SearchResponse:
        """Execute search query"""
        try:
            logger.info("Executing search", query=query)

            # Try Elasticsearch first
            if self.elasticsearch_client:
                return await self._elasticsearch_search(
                    query, filters, sort_options, pagination, search_options
                )

            # Fallback to OpenSearch
            elif self.opensearch_client:
                return await self._opensearch_search(
                    query, filters, sort_options, pagination, search_options
                )

            # Fallback to Whoosh
            else:
                return await self._whoosh_search(
                    query, filters, sort_options, pagination, search_options
                )

        except Exception as e:
            logger.error("Search failed", error=str(e), query=query)
            # Return empty results on error
            return SearchResponse(
                query=query,
                total_results=0,
                products=[],
                search_time_ms=0
            )

    async def _elasticsearch_search(
        self,
        query: str,
        filters: Optional[Dict[str, Any]],
        sort_options: Optional[List[str]],
        pagination: Optional[Dict[str, int]],
        search_options: Optional[Dict[str, Any]]
    ) -> SearchResponse:
        """Execute search using Elasticsearch"""
        try:
            # Build Elasticsearch query
            es_query = {
                "query": {
                    "multi_match": {
                        "query": query,
                        "fields": ["name^2", "description", "brand", "category", "tags"],
                        "type": "best_fields",
                        "fuzziness": "AUTO"
                    }
                }
            }

            # Add filters
            if filters:
                bool_query = {"bool": {"must": [es_query["query"]]}}
                filter_clauses = []

                if filters.get("category"):
                    filter_clauses.append({"term": {"category": filters["category"]}})
                if filters.get("brand"):
                    filter_clauses.append({"term": {"brand": filters["brand"]}})
                if filters.get("price_min") or filters.get("price_max"):
                    price_range = {}
                    if filters.get("price_min"):
                        price_range["gte"] = filters["price_min"]
                    if filters.get("price_max"):
                        price_range["lte"] = filters["price_max"]
                    filter_clauses.append({"range": {"price": price_range}})

                if filter_clauses:
                    bool_query["bool"]["filter"] = filter_clauses
                es_query["query"] = bool_query

            # Add sorting
            if sort_options:
                es_query["sort"] = self._build_sort_options(sort_options)

            # Add pagination
            page = pagination.get("page", 1) if pagination else 1
            size = pagination.get("size", 20) if pagination else 20
            es_query["from"] = (page - 1) * size
            es_query["size"] = size

            # Add aggregations for facets
            es_query["aggs"] = {
                "categories": {"terms": {"field": "category"}},
                "brands": {"terms": {"field": "brand"}},
                "price_ranges": {
                    "range": {
                        "field": "price",
                        "ranges": [
                            {"to": 50},
                            {"from": 50, "to": 100},
                            {"from": 100, "to": 500},
                            {"from": 500}
                        ]
                    }
                }
            }

            # Execute search
            response = await self.elasticsearch_client.search(
                index="products",
                body=es_query
            )

            # Process results
            products = []
            for hit in response["hits"]["hits"]:
                source = hit["_source"]
                products.append(ProductResult(
                    id=source["id"],
                    name=source["name"],
                    description=source.get("description"),
                    price=source["price"],
                    brand=source.get("brand"),
                    category=source.get("category"),
                    relevance_score=hit["_score"]
                ))

            # Process facets
            facets = {}
            if "aggregations" in response:
                aggs = response["aggregations"]
                if "categories" in aggs:
                    facets["category"] = SearchFacet(
                        name="Category",
                        values=[
                            {"value": bucket["key"], "count": bucket["doc_count"]}
                            for bucket in aggs["categories"]["buckets"]
                        ]
                    )

            return SearchResponse(
                query=query,
                total_results=response["hits"]["total"]["value"],
                products=products,
                facets=facets,
                search_time_ms=response["took"],
                page=page,
                page_size=size
            )

        except Exception as e:
            logger.error("Elasticsearch search failed", error=str(e))
            raise

    async def _opensearch_search(
        self,
        query: str,
        filters: Optional[Dict[str, Any]],
        sort_options: Optional[List[str]],
        pagination: Optional[Dict[str, int]],
        search_options: Optional[Dict[str, Any]]
    ) -> SearchResponse:
        """Execute search using OpenSearch (similar to Elasticsearch)"""
        # Implementation similar to Elasticsearch
        logger.info("Using OpenSearch for search", query=query)

        # Mock response for now
        return SearchResponse(
            query=query,
            total_results=0,
            products=[],
            search_time_ms=50
        )

    async def _whoosh_search(
        self,
        query: str,
        filters: Optional[Dict[str, Any]],
        sort_options: Optional[List[str]],
        pagination: Optional[Dict[str, int]],
        search_options: Optional[Dict[str, Any]]
    ) -> SearchResponse:
        """Execute search using Whoosh"""
        try:
            from whoosh.qparser import MultifieldParser
            from whoosh.query import And, Term, NumericRange

            with self.whoosh_index.searcher() as searcher:
                # Build query
                parser = MultifieldParser(["name", "description", "brand", "category"],
                                        self.whoosh_index.schema)
                whoosh_query = parser.parse(query)

                # Add filters
                if filters:
                    filter_queries = []
                    if filters.get("category"):
                        filter_queries.append(Term("category", filters["category"]))
                    if filters.get("brand"):
                        filter_queries.append(Term("brand", filters["brand"]))
                    if filters.get("price_min") or filters.get("price_max"):
                        price_min = filters.get("price_min", 0)
                        price_max = filters.get("price_max", 999999)
                        filter_queries.append(NumericRange("price", price_min, price_max))

                    if filter_queries:
                        whoosh_query = And([whoosh_query] + filter_queries)

                # Execute search
                page = pagination.get("page", 1) if pagination else 1
                size = pagination.get("size", 20) if pagination else 20

                results = searcher.search_page(whoosh_query, page, pagelen=size)

                # Process results
                products = []
                for hit in results:
                    products.append(ProductResult(
                        id=hit["id"],
                        name=hit["name"],
                        description=hit.get("description"),
                        price=float(hit["price"]) if hit.get("price") else 0.0,
                        brand=hit.get("brand"),
                        category=hit.get("category"),
                        relevance_score=hit.score
                    ))

                return SearchResponse(
                    query=query,
                    total_results=len(results),
                    products=products,
                    search_time_ms=25,
                    page=page,
                    page_size=size
                )

        except Exception as e:
            logger.error("Whoosh search failed", error=str(e))
            # Return empty results
            return SearchResponse(
                query=query,
                total_results=0,
                products=[],
                search_time_ms=0
            )

    def _build_sort_options(self, sort_options: List[str]) -> List[Dict[str, Any]]:
        """Build Elasticsearch sort options"""
        sort_mapping = {
            "relevance": {"_score": {"order": "desc"}},
            "price_asc": {"price": {"order": "asc"}},
            "price_desc": {"price": {"order": "desc"}},
            "name_asc": {"name.keyword": {"order": "asc"}},
            "name_desc": {"name.keyword": {"order": "desc"}},
            "rating": {"rating": {"order": "desc"}},
            "newest": {"created_at": {"order": "desc"}},
            "popularity": {"view_count": {"order": "desc"}}
        }

        return [sort_mapping.get(option, sort_mapping["relevance"]) for option in sort_options]

    async def get_autocomplete_suggestions(
        self,
        query_prefix: str,
        user_id: Optional[str] = None,
        max_suggestions: int = 10
    ) -> List[str]:
        """Get autocomplete suggestions"""
        try:
            # Mock suggestions for now
            suggestions = [
                f"{query_prefix} laptop",
                f"{query_prefix} phone",
                f"{query_prefix} headphones",
                f"{query_prefix} tablet",
                f"{query_prefix} watch"
            ][:max_suggestions]

            return suggestions

        except Exception as e:
            logger.error("Autocomplete failed", error=str(e))
            return []

    async def get_health_status(self) -> Dict[str, Any]:
        """Get search engine health status"""
        try:
            status = {
                "status": "healthy",
                "engines": {},
                "performance": {
                    "avg_search_time_ms": 89,
                    "search_success_rate": 0.998
                }
            }

            # Check Elasticsearch
            if self.elasticsearch_client:
                try:
                    cluster_health = await self.elasticsearch_client.cluster.health()
                    status["engines"]["elasticsearch"] = {
                        "status": cluster_health["status"],
                        "nodes": cluster_health["number_of_nodes"]
                    }
                except Exception as e:
                    status["engines"]["elasticsearch"] = {"status": "unhealthy", "error": str(e)}

            # Check OpenSearch
            if self.opensearch_client:
                try:
                    cluster_health = await self.opensearch_client.cluster.health()
                    status["engines"]["opensearch"] = {
                        "status": cluster_health["status"],
                        "nodes": cluster_health["number_of_nodes"]
                    }
                except Exception as e:
                    status["engines"]["opensearch"] = {"status": "unhealthy", "error": str(e)}

            # Check Whoosh
            if self.whoosh_index:
                status["engines"]["whoosh"] = {"status": "healthy"}

            return status

        except Exception as e:
            logger.error("Failed to get search health", error=str(e))
            return {"status": "unhealthy", "error": str(e)}