using WorkOrderService.Infrastructure.Messaging;

namespace WorkOrderService.Infrastructure.IntegrationTests;

public sealed class RabbitMqOptionsTests
{
    [Fact]
    public void ToString_DoesNotExposePassword()
    {
        var options = new RabbitMqOptions("localhost", 5672, "sastreria", "test-secret");

        var text = options.ToString();

        Assert.DoesNotContain("test-secret", text);
        Assert.Contains("[redacted]", text);
    }
}
