using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using ResultCrafter.MediatR;

namespace ResultCrafter.Tests.MediatR;

/// <summary>
///     Exercises the actual MediatR DI registration end-to-end. The unit tests for the behavior
///     call <c>Handle</c> directly, so they never caught that registering the validation behavior
///     threw at <c>BuildServiceProvider()</c>. These tests build the real container and send real
///     requests through the pipeline.
/// </summary>
public sealed class MediatRRegistrationTests
{
    [Fact]
    public void AddResultCrafterValidation_BuildsContainer_WithoutArityError()
    {
        // Regression: a behavior fixing TResponse to the void Result is arity-1 and cannot be
        // registered against the arity-2 IPipelineBehavior<,>; doing so threw here at build time.
        using var provider = BuildProvider();

        Assert.NotNull(provider.GetService<IMediator>());
    }

    [Fact]
    public async Task Send_VoidResultRequest_ShortCircuitsOnInvalid()
    {
        using var provider = BuildProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        var result = await mediator.Send(new RegDeleteCommand(0), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.BadRequest, result.Error!.Value.Type);
    }

    [Fact]
    public async Task Send_ResultOfTRequest_ShortCircuitsOnInvalid()
    {
        using var provider = BuildProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        var result = await mediator.Send(new RegValueQuery(0), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.BadRequest, result.Error!.Value.Type);
    }

    [Fact]
    public async Task Send_ValidRequest_ReachesHandler()
    {
        using var provider = BuildProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        var result = await mediator.Send(new RegValueQuery(5), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(5, result.Value);
    }

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<MediatRRegistrationTests>();
            cfg.AddResultCrafterValidation();
        });
        services.AddSingleton<IValidator<RegDeleteCommand>, RegDeleteCommandValidator>();
        services.AddSingleton<IValidator<RegValueQuery>, RegValueQueryValidator>();
        return services.BuildServiceProvider(true);
    }
}

// Top-level types so MediatR's assembly scan discovers the handlers.
public sealed record RegDeleteCommand(int Id) : IRequest<Result>;

public sealed record RegValueQuery(int Id) : IRequest<Result<int>>;

public sealed class RegDeleteCommandHandler : IRequestHandler<RegDeleteCommand, Result>
{
    public Task<Result> Handle(RegDeleteCommand request, CancellationToken cancellationToken)
    {
        return Task.FromResult(Result.NoContent());
    }
}

public sealed class RegValueQueryHandler : IRequestHandler<RegValueQuery, Result<int>>
{
    public Task<Result<int>> Handle(RegValueQuery request, CancellationToken cancellationToken)
    {
        return Task.FromResult(Result<int>.Ok(request.Id));
    }
}

public sealed class RegDeleteCommandValidator : AbstractValidator<RegDeleteCommand>
{
    public RegDeleteCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
    }
}

public sealed class RegValueQueryValidator : AbstractValidator<RegValueQuery>
{
    public RegValueQueryValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
    }
}
