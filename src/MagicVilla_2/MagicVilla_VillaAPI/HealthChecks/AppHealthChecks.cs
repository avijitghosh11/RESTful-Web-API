﻿using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace MagicVilla_VillaAPI.HealthChecks
{
	public class AppHealthChecks : IHealthCheck
	{
		private Random _random = new Random();
		public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, 
			CancellationToken cancellationToken = default)
		{
			var responseTime = _random.Next(1, 300);
			if (responseTime < 100)
			{
				return Task.FromResult(HealthCheckResult.Healthy("Healthy result from CustomHealthChecks"));
			}
			else if (responseTime < 200)
			{
				return Task.FromResult(HealthCheckResult.Degraded("Degraded result from CustomHealthChecks"));
			}

			return Task.FromResult(HealthCheckResult.Unhealthy("Unhealthy result from CustomHealthChecks"));
		}
	}
}
