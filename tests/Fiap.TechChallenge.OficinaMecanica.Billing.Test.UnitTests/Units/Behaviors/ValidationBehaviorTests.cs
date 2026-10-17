using FluentAssertions;
using FluentValidation;
using Fiap.TechChallenge.OficinaMecanica.Billing.Application;
using Fiap.TechChallenge.OficinaMecanica.Billing.Application.Exceptions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Fiap.TechChallenge.OficinaMecanica.Billing.Test.UnitTests.Units.Behaviors;

public class ValidationBehaviorTests
{
    [Fact]
    public async Task Send_DeveFalhar_QuandoRequestForInvalido()
    {
        TestCommandHandler.ExecutionCount = 0;

        var services = new ServiceCollection();

        services.AddLogging();
        services.AddApplication();
        services.AddMediatR(config =>
            config.RegisterServicesFromAssembly(typeof(TestCommand).Assembly));
        services.AddScoped<IValidator<TestCommand>, TestCommandValidator>();

        using var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        var action = async () => await mediator.Send(new TestCommand { Nome = string.Empty });

        await action.Should()
            .ThrowAsync<ServiceValidationException>()
            .WithMessage("*Nome e obrigatorio.*");

        TestCommandHandler.ExecutionCount.Should().Be(0);
    }

    [Fact]
    public async Task Send_DeveExecutarHandler_QuandoRequestForValido()
    {
        TestCommandHandler.ExecutionCount = 0;

        var services = new ServiceCollection();

        services.AddLogging();
        services.AddApplication();
        services.AddMediatR(config =>
            config.RegisterServicesFromAssembly(typeof(TestCommand).Assembly));
        services.AddScoped<IValidator<TestCommand>, TestCommandValidator>();

        using var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        var result = await mediator.Send(new TestCommand { Nome = "Lucas" });

        result.Should().Be("ok");
        TestCommandHandler.ExecutionCount.Should().Be(1);
    }

    public sealed class TestCommand : IRequest<string>
    {
        public string Nome { get; init; } = string.Empty;
    }

    public sealed class TestCommandValidator : AbstractValidator<TestCommand>
    {
        public TestCommandValidator()
        {
            RuleFor(x => x.Nome)
                .NotEmpty()
                .WithMessage("Nome e obrigatorio.");
        }
    }

    public sealed class TestCommandHandler : IRequestHandler<TestCommand, string>
    {
        public static int ExecutionCount { get; set; }

        public Task<string> Handle(TestCommand request, CancellationToken cancellationToken)
        {
            ExecutionCount++;
            return Task.FromResult("ok");
        }
    }
}
