import { App, Button, Card, Form, Input } from "antd";
import { useState } from "react";

import type { Profile, User } from "../../types";
import { api, errorMessage } from "../api";

export default function Settings({ user, onSaved }: { user: User; onSaved: (user: User) => void }) {
  const { message } = App.useApp();
  const [busy, setBusy] = useState(false);

  async function save(profile: Profile) {
    setBusy(true);
    try {
      onSaved(await api.updateProfile(profile));
      message.success("Profile saved");
    } catch (failure) {
      message.error(errorMessage(failure));
    } finally {
      setBusy(false);
    }
  }

  return (
    <Card title="Settings" style={{ maxWidth: 560 }}>
      <Form layout="vertical" requiredMark={false} initialValues={user} onFinish={save}>
        <Form.Item name="name" label="Full Name" rules={[{ required: true, whitespace: true, max: 100, message: "Enter your full name" }]}>
          <Input maxLength={100} placeholder="Your full name" />
        </Form.Item>
        <Form.Item label="Email" htmlFor="settings-email">
          <Input id="settings-email" value={user.email} disabled />
        </Form.Item>
        <Form.Item name="phone" label="Phone">
          <Input maxLength={30} placeholder="+998 90 123 45 67" autoComplete="tel" />
        </Form.Item>
        <Form.Item name="address" label="Address">
          <Input.TextArea maxLength={300} rows={3} placeholder="City, street, house" autoComplete="street-address" />
        </Form.Item>
        <Button type="primary" htmlType="submit" loading={busy}>
          Save
        </Button>
      </Form>
    </Card>
  );
}
