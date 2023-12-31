﻿using AICentral.Core;
using AICentral.Steps.Endpoints;

namespace AICentral.Steps.EndpointSelectors.Random;

public class RandomEndpointSelector : EndpointSelectorBase
{
    private readonly System.Random _rnd = new(Environment.TickCount);
    private readonly IAICentralEndpointDispatcher[] _openAiServers;

    public RandomEndpointSelector(IAICentralEndpointDispatcher[] openAiServers)
    {
        _openAiServers = openAiServers;
    }

    public override async Task<AICentralResponse> Handle(HttpContext context,
        AICallInformation aiCallInformation,
        CancellationToken cancellationToken)
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<RandomEndpointSelectorBuilder>>();
        var toTry = _openAiServers.ToList();
        logger.LogDebug("Random Endpoint selector is handling request");
        do
        {
            var chosen = toTry.ElementAt(_rnd.Next(0, toTry.Count));
            toTry.Remove(chosen);
            try
            {
                var responseMessage =
                    await chosen.Handle(context, aiCallInformation, cancellationToken); //awaiting to unwrap any Aggregate Exceptions
                return await HandleResponse(
                    logger,
                    context,
                    chosen,
                    responseMessage.Item1,
                    responseMessage.Item2,
                    !toTry.Any(),
                    cancellationToken);
            }
            catch (HttpRequestException e)
            {
                if (!toTry.Any())
                {
                    logger.LogError(e, "Failed to handle request. Exhausted endpoints");
                    throw new InvalidOperationException("No available Open AI hosts", e);
                }

                logger.LogWarning(e, "Failed to handle request. Trying another endpoint");
            }
        } while (toTry.Count > 0);

        throw new InvalidOperationException("Failed to satisfy request");
    }

    public override object WriteDebug()
    {
        return new
        {
            Type = "Random Router",
            Endpoints = _openAiServers.Select(x => WriteDebug())
        };
    }
}