using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Jellyfin.Plugin.JellyScout.Models
{
    public class TmdbSearchResponse<T>
    {
        [JsonProperty("page")]
        public int Page { get; set; }

        [JsonProperty("results")]
        public List<T> Results { get; set; } = new();

        [JsonProperty("total_pages")]
        public int TotalPages { get; set; }

        [JsonProperty("total_results")]
        public int TotalResults { get; set; }
    }

    public class TmdbSearchResult
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("media_type")]
        public string MediaType { get; set; } = string.Empty;

        [JsonProperty("title")]
        public string? Title { get; set; }

        [JsonProperty("name")]
        public string? Name { get; set; }

        [JsonProperty("overview")]
        public string? Overview { get; set; }

        [JsonProperty("poster_path")]
        public string? PosterPath { get; set; }

        [JsonProperty("backdrop_path")]
        public string? BackdropPath { get; set; }

        [JsonProperty("popularity")]
        public double Popularity { get; set; }

        [JsonProperty("vote_average")]
        public double VoteAverage { get; set; }

        [JsonProperty("vote_count")]
        public int VoteCount { get; set; }

        [JsonProperty("adult")]
        public bool Adult { get; set; }

        [JsonProperty("original_language")]
        public string OriginalLanguage { get; set; } = string.Empty;

        [JsonProperty("genre_ids")]
        public List<int> GenreIds { get; set; } = new();

        // Movie specific properties
        [JsonProperty("release_date")]
        public string? ReleaseDate { get; set; }

        [JsonProperty("original_title")]
        public string? OriginalTitle { get; set; }

        [JsonProperty("video")]
        public bool Video { get; set; }

        // TV Show specific properties
        [JsonProperty("first_air_date")]
        public string? FirstAirDate { get; set; }

        [JsonProperty("original_name")]
        public string? OriginalName { get; set; }

        [JsonProperty("origin_country")]
        public List<string> OriginCountry { get; set; } = new();

        // Helper properties
        public string DisplayTitle => !string.IsNullOrEmpty(Title) ? Title : Name ?? string.Empty;
        public string? DisplayDate => !string.IsNullOrEmpty(ReleaseDate) ? ReleaseDate : FirstAirDate;
        public DateTime? ParsedDate => DateTime.TryParse(DisplayDate, out var date) ? date : null;
        public string? PosterUrl => !string.IsNullOrEmpty(PosterPath) ? $"https://image.tmdb.org/t/p/w500{PosterPath}" : null;
        public string? BackdropUrl => !string.IsNullOrEmpty(BackdropPath) ? $"https://image.tmdb.org/t/p/w1280{BackdropPath}" : null;
    }

    public class TmdbMovie : TmdbSearchResult
    {
        public TmdbMovie()
        {
            MediaType = "movie";
        }
    }

    public class TmdbTvShow : TmdbSearchResult
    {
        public TmdbTvShow()
        {
            MediaType = "tv";
        }
    }
} 