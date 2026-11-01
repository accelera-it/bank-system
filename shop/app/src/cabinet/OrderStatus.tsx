import { CheckCircleOutlined, ClockCircleOutlined, SyncOutlined } from "@ant-design/icons";
import { Tag } from "antd";
import type { ReactNode } from "react";

import type { PaymentStatus } from "../types";

const STATUS: Record<PaymentStatus, { color: string; icon: ReactNode; label: string }> = {
  pending: { color: "gold", icon: <ClockCircleOutlined />, label: "Pending" },
  processing: { color: "blue", icon: <SyncOutlined />, label: "Processing" },
  completed: { color: "green", icon: <CheckCircleOutlined />, label: "Paid" },
};

export default function OrderStatus({ status }: { status: PaymentStatus }) {
  const { color, icon, label } = STATUS[status];
  return (
    <Tag color={color} icon={icon}>
      {label}
    </Tag>
  );
}
