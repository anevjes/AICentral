﻿namespace AICentral.PipelineComponents.Endpoints.ResultHandlers;

public class StreamingResultHandler: IResult
{
    public Task ExecuteAsync(HttpContext context)
    {
        //assume result already sent down by the time this runs. No-op.
        return Task.CompletedTask;
    }
}