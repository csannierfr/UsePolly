using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BlazorApp.Models;
using BlazorApp.Pages;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Wrap;

namespace BlazorApp.Services
{
    public interface IWeatherForecastHttpRepository
    {
        Task<List<WeatherForecast>> GetWeatherForecast();
    }

    public class WeatherForecastHttpRepository : IWeatherForecastHttpRepository
    {

        private readonly HttpClient _client;

        private static List<WeatherForecast> _lastWeatherForecasts = new List<WeatherForecast>();

        private static readonly AsyncCircuitBreakerPolicy CircuitBreakerPolicy = Policy
            .Handle<Exception>()
            .CircuitBreakerAsync(
                2, //Nombre d'exceptions avant le passage en mode open
                TimeSpan.FromSeconds(30), //Temps avant d'essayer de rét
                OnBreak, 
                OnReset);

        private readonly AsyncPolicyWrap<List<WeatherForecast>> _processHttpMessagePolicy;
        
        private readonly ILogger<WeatherForecastHttpRepository> _logger;

        public WeatherForecastHttpRepository(HttpClient client, ILogger<WeatherForecastHttpRepository> logger)
        {
            _client = client;
            _logger = logger;

            _processHttpMessagePolicy = Policy<List<WeatherForecast>>
                .Handle<Exception>()
                .FallbackAsync<List<WeatherForecast>>(fallbackAction: async ct =>
                        _lastWeatherForecasts,
                    onFallbackAsync: async e => Console.WriteLine("Return Fallback")).WrapAsync(CircuitBreakerPolicy);
        }


        public async Task<List<WeatherForecast>> GetWeatherForecast()
        {

            try
            {
                Console.WriteLine($"Circuit Breaker status {CircuitBreakerPolicy.CircuitState}");
                var response = await _processHttpMessagePolicy.ExecuteAsync(() => ExecuteGetRequest());
                _lastWeatherForecasts = response;

                return response;

            }
            catch (Exception ex)
            {
                return new List<WeatherForecast>();
            }

            
        }

        private async Task<List<WeatherForecast>> ExecuteGetRequest()
        {
            Console.WriteLine("Call Api");
            return await _client.GetFromJsonAsync<List<WeatherForecast>>("/WeatherForecast");
        }

        private static List<WeatherForecast> GetCacheResponse()
        {
            return _lastWeatherForecasts;
        }


        private static void OnReset()
        {
            Console.WriteLine("Circuit is on Reset");
        }

        private static void OnBreak(Exception arg1, TimeSpan arg2)
        {

            Console.WriteLine("Circuit is on Break");
        }
    }
}