import { App as AntApp, ConfigProvider, Flex, Spin } from "antd";
import { useEffect, useState } from "react";
import { Navigate, Route, Routes } from "react-router-dom";

import type { User } from "../types";
import { api } from "./api";
import CabinetLayout from "./CabinetLayout";
import Dashboard from "./pages/Dashboard";
import Orders from "./pages/Orders";
import Products from "./pages/Products";
import Settings from "./pages/Settings";

function Cabinet() {
  const [user, setUser] = useState<User | null>(null);

  useEffect(() => {
    api.me().then(setUser).catch(() => {});
  }, []);

  async function logout() {
    const { url } = await api.logout();
    window.location.assign(url);
  }

  if (!user) {
    return (
      <Flex align="center" justify="center" style={{ minHeight: "100vh" }}>
        <Spin size="large" />
      </Flex>
    );
  }

  return (
    <Routes>
      <Route element={<CabinetLayout user={user} onLogout={logout} />}>
        <Route index element={<Navigate to="/cabinet/dashboard" replace />} />
        <Route path="dashboard" element={<Dashboard />} />
        <Route path="orders" element={<Orders />} />
        <Route path="products" element={<Products />} />
        <Route path="settings" element={<Settings user={user} onSaved={setUser} />} />
        <Route path="*" element={<Navigate to="/cabinet/dashboard" replace />} />
      </Route>
    </Routes>
  );
}

export default function CabinetApp() {
  return (
    <ConfigProvider theme={{ token: { colorPrimary: "#0b7f71", borderRadius: 8 } }}>
      <AntApp>
        <Cabinet />
      </AntApp>
    </ConfigProvider>
  );
}
