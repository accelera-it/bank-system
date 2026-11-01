import { AppstoreOutlined, DashboardOutlined, EditOutlined, KeyOutlined, LogoutOutlined, ShoppingCartOutlined } from "@ant-design/icons";
import { Button, Flex, Layout, Menu, Typography } from "antd";
import { Link, Outlet, useLocation, useNavigate } from "react-router-dom";

import type { User } from "../types";

// Profile and password live in Keycloak
const EDIT_PROFILE = "/api/auth/account";
const CHANGE_PASSWORD = "/api/auth/account?password=true";

const ITEMS = [
  {
    type: "group" as const,
    label: "Overview",
    children: [{ key: "/cabinet/dashboard", icon: <DashboardOutlined />, label: "Dashboard" }],
  },
  {
    type: "group" as const,
    label: "Shopping",
    children: [
      { key: "/cabinet/orders", icon: <ShoppingCartOutlined />, label: "Orders" },
      { key: "/cabinet/products", icon: <AppstoreOutlined />, label: "Products" },
    ],
  },
  {
    type: "group" as const,
    label: "Settings",
    children: [
      { key: EDIT_PROFILE, icon: <EditOutlined />, label: "Edit profile" },
      { key: CHANGE_PASSWORD, icon: <KeyOutlined />, label: "Change password" },
    ],
  },
];

interface Props {
  user: User;
  onLogout: () => void;
}

export default function CabinetLayout({ user, onLogout }: Props) {
  const location = useLocation();
  const navigate = useNavigate();

  return (
    <Layout style={{ minHeight: "100vh" }}>
      <Layout.Sider breakpoint="md" collapsedWidth={0} theme="light" style={{ position: "sticky", top: 0, height: "100vh" }}>
        <Flex vertical style={{ height: "100%" }}>
          <Link to="/" style={{ color: "inherit" }}>
            <Flex align="center" gap={8} style={{ height: 64, padding: "0 20px" }}>
              <img src="/img/logo.svg" alt="" width={32} height={32} />
              <Typography.Text strong style={{ fontSize: 20 }}>
                Shop
              </Typography.Text>
            </Flex>
          </Link>
          <Menu
            mode="inline"
            selectedKeys={[location.pathname]}
            items={ITEMS}
            onClick={({ key }) => (key.startsWith("/api/") ? window.location.assign(key) : navigate(key))}
            style={{ borderInlineEnd: 0, flex: 1 }}
          />
          <Flex vertical gap={12} style={{ padding: 16 }}>
            <Typography.Text strong ellipsis>
              {user.name || user.email}
            </Typography.Text>
            <Button type="primary" danger block icon={<LogoutOutlined />} onClick={onLogout}>
              Log out
            </Button>
          </Flex>
        </Flex>
      </Layout.Sider>
      <Layout>
        <Layout.Content style={{ padding: 24 }}>
          <Outlet />
        </Layout.Content>
      </Layout>
    </Layout>
  );
}
