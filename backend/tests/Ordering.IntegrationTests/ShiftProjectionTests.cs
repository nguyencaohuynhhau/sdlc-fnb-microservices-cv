using System.Net;
using System.Net.Http.Json;
using Confluent.Kafka;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Ordering.Application;
using Ordering.Infrastructure;
using Shared.Messaging;

namespace Ordering.IntegrationTests;

[Trait("Category", "Integration")]
[Collection(OrderingCollection.Name)]
public sealed class ShiftProjectionTests(OrderingFixture fx) : IAsyncLifetime
{
    private readonly HttpClient _cashier = fx.ClientAs("Cashier");
    private readonly HttpClient _kitchen = fx.ClientAs("Kitchen");

    public Task InitializeAsync() => fx.CloseAllShiftsAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private Task<HttpResponseMessage> PostOrder() =>
        _cashier.PostAsJsonAsync("/api/orders", new { items = new[] { new { menuItemId = fx.Latte.Id, qty = 1 } } });

    [Fact]
    public async Task OrderCreation_WithoutOpenShift_Returns409()
    {
        var res = await PostOrder();

        res.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await res.Content.ReadFromJsonAsync<ProblemDetails>())!.Detail.Should().Be("Chưa mở ca làm việc. Mở ca trước khi nhận đơn.");
        (await _kitchen.GetAsync("/api/orders/current-shift")).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task OrderCreation_AfterShiftOpenedEvent_Succeeds()
    {
        // Payload sinh bởi CHÍNH record của cashier — test hợp đồng giữa hai dịch vụ, đi qua Kafka thật.
        var opened = new Cashier.Domain.ShiftOpened(Guid.NewGuid(), DateTimeOffset.UtcNow);
        var message = OutboxInterceptor.ToOutbox(opened);
        using (var producer = new ProducerBuilder<string, string>(new ProducerConfig { BootstrapServers = fx.KafkaBootstrap }).Build())
        {
            await producer.ProduceAsync(message.Topic, new Message<string, string> { Key = message.Key, Value = message.Payload });
        }

        var current = await WaitForCurrentShiftAsync(opened.ShiftId, TimeSpan.FromSeconds(60));
        current.Should().NotBeNull("ordering phải nhận ShiftOpened qua Kafka và cập nhật hình chiếu ca");

        var res = await PostOrder();
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        (await res.Content.ReadFromJsonAsync<OrderView>())!.ShiftId.Should().Be(opened.ShiftId);
    }

    [Fact]
    public async Task ShiftClosedArrivingBeforeOpened_ShiftStaysClosed()
    {
        // Mở và đóng ca đi trên hai topic khác nhau — thứ tự tới không được đảm bảo.
        var scopes = fx.Factory.Services.GetRequiredService<IServiceScopeFactory>();
        var id = Guid.NewGuid();
        var openedAt = DateTimeOffset.UtcNow;

        await KafkaConsumerHost<OrderingDbContext, ShiftClosed>.ProcessAsync(scopes, new ShiftClosed(id, openedAt.AddHours(8)), default);
        await KafkaConsumerHost<OrderingDbContext, ShiftOpened>.ProcessAsync(scopes, new ShiftOpened(id, openedAt), default);

        (await _kitchen.GetAsync("/api/orders/current-shift")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await PostOrder()).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    private async Task<CurrentShiftView?> WaitForCurrentShiftAsync(Guid shiftId, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var res = await _kitchen.GetAsync("/api/orders/current-shift");
            if (res.StatusCode == HttpStatusCode.OK && await res.Content.ReadFromJsonAsync<CurrentShiftView>() is { } s && s.ShiftId == shiftId)
            {
                return s;
            }

            await Task.Delay(200);
        }

        return null;
    }
}
