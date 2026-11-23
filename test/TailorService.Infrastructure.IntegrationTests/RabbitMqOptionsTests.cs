using TailorService.Infrastructure.Messaging;

namespace TailorService.Infrastructure.IntegrationTests;

public sealed class RabbitMqOptionsTests
{
    [Fact]
    public void ToString_DoesNotRevealThePassword()
    {
        var text = new RabbitMqOptions("localhost", 5672, "sastreria", "s3cret-password").ToString();

        Assert.DoesNotContain("s3cret-password", text);
    }
}
