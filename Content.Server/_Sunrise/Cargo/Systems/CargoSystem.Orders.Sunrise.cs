using System.Diagnostics.CodeAnalysis;
using Content.Server.Cargo.Components;
using Content.Shared.Cargo;
using Content.Shared.Cargo.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server.Cargo.Systems;

public sealed partial class CargoSystem
{
    private static List<CargoOrderData> EnsureOrderList(
        StationCargoOrderDatabaseComponent orderDatabase,
        ProtoId<CargoAccountPrototype> account)
    {
        if (!orderDatabase.Orders.TryGetValue(account, out var orders))
        {
            orders = new List<CargoOrderData>();
            orderDatabase.Orders.Add(account, orders);
        }

        return orders;
    }

    private static List<CargoOrderData> GetOrderListOrEmpty(
        StationCargoOrderDatabaseComponent orderDatabase,
        ProtoId<CargoAccountPrototype> account)
    {
        return orderDatabase.Orders.GetValueOrDefault(account) ?? [];
    }

    private static bool TryFindUnapprovedOrder(
        StationCargoOrderDatabaseComponent orderDatabase,
        ProtoId<CargoAccountPrototype> account,
        int orderId,
        [NotNullWhen(true)] out List<CargoOrderData>? orders,
        [NotNullWhen(true)] out CargoOrderData? order)
    {
        if (!orderDatabase.Orders.TryGetValue(account, out orders))
        {
            order = null;
            return false;
        }

        order = orders.Find(order => order.OrderId == orderId && !order.Approved);
        return order != null;
    }

    private static bool TryRemoveOrder(
        StationCargoOrderDatabaseComponent orderDatabase,
        ProtoId<CargoAccountPrototype> account,
        int orderId)
    {
        if (!orderDatabase.Orders.TryGetValue(account, out var orders))
            return false;

        var sequenceIdx = orders.FindIndex(order => order.OrderId == orderId);
        if (sequenceIdx == -1)
            return false;

        orders.RemoveAt(sequenceIdx);
        return true;
    }

    private static bool TryPopFrontOrder(
        StationCargoOrderDatabaseComponent orderDatabase,
        ProtoId<CargoAccountPrototype> account,
        [NotNullWhen(true)] out CargoOrderData? orderOut)
    {
        if (!orderDatabase.Orders.TryGetValue(account, out var orders))
        {
            orderOut = null;
            return false;
        }

        var orderIdx = orders.FindIndex(order => order.Approved);
        if (orderIdx == -1)
        {
            orderOut = null;
            return false;
        }

        orderOut = orders[orderIdx];
        orderOut.NumDispatched++;

        if (orderOut.NumDispatched >= orderOut.OrderQuantity)
        {
            // Заказ завершён, удаляем его из очереди.
            orders.RemoveAt(orderIdx);
        }

        return true;
    }
}
