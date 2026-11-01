import { CheckCircleOutlined, ClockCircleOutlined, ShoppingCartOutlined, WalletOutlined } from "@ant-design/icons";
import { App, Card, Col, Flex, Row, Statistic, Table, type TableColumnsType } from "antd";
import { useEffect, useState } from "react";
import { Link } from "react-router-dom";

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

  const paid = orders?.filter((order) => order.payment_status === "completed") ?? [];
  const spent = paid.reduce((total, order) => total + order.product.price, 0);
  const loading = orders === null;
  const stats = [
    { title: "Orders", value: orders?.length ?? 0, icon: <ShoppingCartOutlined /> },
    { title: "Paid", value: paid.length, icon: <CheckCircleOutlined /> },
    { title: "Pending", value: (orders?.length ?? 0) - paid.length, icon: <ClockCircleOutlined /> },
    { title: "Total spent", value: formatPrice(spent), icon: <WalletOutlined /> },
  ];

  return (
    <Flex vertical gap={24}>
      <Row gutter={[16, 16]}>
        {stats.map(({ title, value, icon }) => (
          <Col key={title} xs={24} sm={12} xl={6}>
            <Card>
              <Statistic title={title} value={value} prefix={icon} loading={loading} />
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
