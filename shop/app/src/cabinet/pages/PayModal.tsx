import { BankOutlined, CreditCardOutlined } from "@ant-design/icons";
import { Alert, Button, Form, Input, Modal, Tabs, Typography } from "antd";
import { useState } from "react";

import type { Card, Order } from "../../types";
import { api, errorMessage, formatPrice, postRedirect } from "../api";

function BankPayment({ order }: { order: Order }) {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");

  async function pay() {
    setBusy(true);
    setError("");
    try {
      postRedirect(await api.directPayment(order.id));
    } catch (failure) {
      setError(errorMessage(failure));
      setBusy(false);
    }
  }

  return (
    <>
      <Typography.Paragraph type="secondary">You will confirm the payment on your bank's page, then come back here.</Typography.Paragraph>
      {error && <Alert type="error" title={error} showIcon style={{ marginBottom: 16 }} />}
      <Button type="primary" block icon={<BankOutlined />} loading={busy} onClick={pay}>
        Continue to the bank
      </Button>
    </>
  );
}

function CardPayment({ order, onPaid }: { order: Order; onPaid: (order: Order) => void }) {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");

  async function pay(card: Card) {
    setBusy(true);
    setError("");
    try {
      onPaid(await api.cardPayment(order.id, card));
    } catch (failure) {
      setError(errorMessage(failure));
      setBusy(false);
    }
  }

  return (
    <Form layout="vertical" requiredMark={false} onFinish={pay}>
      {error && <Alert type="error" title={error} showIcon style={{ marginBottom: 16 }} />}
      <Form.Item
        name="number"
        label="Card number"
        rules={[{ required: true, pattern: /^\d{16}$/, message: "Enter the 16 digits of the card" }]}
      >
        <Input inputMode="numeric" maxLength={16} autoComplete="cc-number" />
      </Form.Item>
      <Form.Item name="name" label="Name on card" rules={[{ required: true, whitespace: true, max: 50, message: "Enter the name on the card" }]}>
        <Input maxLength={50} autoComplete="cc-name" />
      </Form.Item>
      <Form.Item
        name="expiry_date"
        label="Expires"
        rules={[{ required: true, pattern: /^(0[1-9]|1[0-2])\/\d{2}$/, message: "Use the MM/YY format" }]}
      >
        <Input placeholder="MM/YY" maxLength={5} autoComplete="cc-exp" />
      </Form.Item>
      <Form.Item name="security_code" label="CVV" rules={[{ required: true, pattern: /^\d{3}$/, message: "Enter the 3 digits" }]}>
        <Input.Password inputMode="numeric" maxLength={3} autoComplete="cc-csc" />
      </Form.Item>
      <Button type="primary" htmlType="submit" block loading={busy}>
        Pay {formatPrice(order.product.price)}
      </Button>
    </Form>
  );
}

interface Props {
  order: Order;
  onClose: () => void;
  onPaid: (order: Order) => void;
}

export default function PayModal({ order, onClose, onPaid }: Props) {
  return (
    <Modal open title={`Pay for ${order.product.name}`} footer={null} onCancel={onClose} destroyOnHidden>
      <Tabs
        items={[
          { key: "bank", label: "Bank", icon: <BankOutlined />, children: <BankPayment order={order} /> },
          { key: "card", label: "Card", icon: <CreditCardOutlined />, children: <CardPayment order={order} onPaid={onPaid} /> },
        ]}
      />
    </Modal>
  );
}
