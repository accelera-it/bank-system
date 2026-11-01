import { App, Alert, Button, Card, Col, Flex, Row, Statistic, Table, type TableColumnsType } from "antd";
import { useEffect, useState } from "react";
import { Link, useNavigate } from "react-router-dom";

import type { Order } from "../../types";
import { api, formatPrice } from "../api";
import OrderStatus from "../OrderStatus";

const RECENT_ORDERS = 5;

const columns: TableColumnsType<Order> = [
  { title: "Product", render: (_, order) => order.product.name },
  { title: "Price", render: (_, order) => formatPrice(order.product.price) },
  { title: "Status", dataIndex: "payment_status", render: (status) => <OrderStatus status={status} /> },
];

export default function Dashboard() {
  const { message } = App.useApp();
  const navigate = useNavigate();
  const [orders, setOrders] = useState<Order[] | null>(null);

  useEffect(() => {
    api
      .orders()
      .then(setOrders)
      .catch((error: Error) => {
        setOrders([]);
        message.error(error.message);
      });
  }, [message]);

  const loading = orders === null;
  const paid = orders?.filter((order) => order.payment_status === "completed") ?? [];
  const pending = orders?.filter((order) => order.payment_status === "pending") ?? [];
  const spent = paid.reduce((total, order) => total + order.product.price, 0);
  const stats = [
    { title: "Total spent", value: formatPrice(spent) },
    { title: "Paid", value: paid.length },
    { title: "Pending", value: pending.length },
  ];

  return (
    <Flex vertical gap={24}>
      {pending.length > 0 && (
        <Alert
          type="warning"
          showIcon
          title={`${pending.length} ${pending.length === 1 ? "order is" : "orders are"} waiting for payment`}
          action={
            <Button type="primary" size="small" onClick={() => navigate(`/cabinet/orders?pay=${pending[0].id}`)}>
              Pay now
            </Button>
          }
        />
      )}
      <Row gutter={[16, 16]}>
        {stats.map(({ title, value }) => (
          <Col key={title} xs={24} sm={8}>
            <Card style={{ height: "100%" }}>
              <Statistic title={title} value={value} loading={loading} />
            </Card>
          </Col>
        ))}
      </Row>
      <Card title="Recent orders" extra={<Link to="/cabinet/orders">View all</Link>}>
        <Table
          rowKey="id"
          loading={loading}
          dataSource={orders?.slice(0, RECENT_ORDERS) ?? []}
          columns={columns}
          pagination={false}
          scroll={{ x: "max-content" }}
          locale={{ emptyText: "You have no orders yet" }}
        />
      </Card>
    </Flex>
  );
}
