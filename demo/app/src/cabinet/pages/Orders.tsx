import { CreditCardOutlined } from "@ant-design/icons";
import { App, Button, Card, Table, type TableColumnsType } from "antd";
import { useCallback, useEffect, useState } from "react";
import { useSearchParams } from "react-router-dom";

import type { Order, PaymentStatus } from "../../types";
import { api, formatPrice } from "../api";
import OrderStatus from "../OrderStatus";
import PayModal from "./PayModal";

export default function Orders() {
  const { message } = App.useApp();
  const [params, setParams] = useSearchParams();
  const [orders, setOrders] = useState<Order[] | null>(null);

  const load = useCallback(() => {
    api
      .orders()
      .then(setOrders)
      .catch((error: Error) => {
        setOrders([]);
        message.error(error.message);
      });
  }, [message]);

  useEffect(load, [load]);

  const paying = orders?.find((order) => order.id === params.get("pay") && order.payment_status === "pending");

  function closePayment() {
    setParams({}, { replace: true });
  }

  const columns: TableColumnsType<Order> = [
    { title: "Product", render: (_, order) => order.product.name },
    { title: "Price", render: (_, order) => formatPrice(order.product.price) },
    { title: "Date", dataIndex: "created_at", render: (date: string) => new Date(date).toLocaleString("en-GB") },
    {
      title: "Status",
      dataIndex: "payment_status",
      render: (status: PaymentStatus) => <OrderStatus status={status} />,
    },
    {
      title: "",
      align: "right",
      render: (_, order) =>
        order.payment_status === "pending" && (
          <Button type="primary" icon={<CreditCardOutlined />} onClick={() => setParams({ pay: order.id })}>
            Pay
          </Button>
        ),
    },
  ];

  return (
    <Card title="Orders">
      <Table
        rowKey="id"
        loading={orders === null}
        dataSource={orders ?? []}
        columns={columns}
        pagination={{ pageSize: 10, hideOnSinglePage: true }}
        scroll={{ x: "max-content" }}
        locale={{ emptyText: "You have no orders yet" }}
      />
      {paying && (
        <PayModal
          order={paying}
          onClose={closePayment}
          onPaid={() => {
            closePayment();
            message.success("Payment completed");
            load();
          }}
        />
      )}
    </Card>
  );
}
