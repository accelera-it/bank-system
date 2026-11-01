import { DeleteOutlined, EditOutlined, PlusOutlined, UploadOutlined } from "@ant-design/icons";
import { App, Button, Card, Flex, Form, Input, InputNumber, Modal, Popconfirm, Table, type TableColumnsType, Upload } from "antd";
import { useCallback, useEffect, useState } from "react";

import type { Product, ProductInput } from "../../types";
import { api, errorMessage, formatPrice } from "../api";

function ImageUpload({ value, onChange }: { value?: string; onChange?: (url: string) => void }) {
  const { message } = App.useApp();
  const [busy, setBusy] = useState(false);

  async function upload(file: File) {
    setBusy(true);
    try {
      const { url } = await api.uploadImage(file);
      onChange?.(url);
    } catch (failure) {
      message.error(errorMessage(failure));
    } finally {
      setBusy(false);
    }
  }

  return (
    <Flex align="center" gap={12}>
      {value && <img src={value} alt="" width={48} height={48} style={{ objectFit: "contain" }} />}
      <Upload
        accept="image/png,image/jpeg,image/webp"
        showUploadList={false}
        beforeUpload={(file) => {
          void upload(file);
          return false;
        }}
      >
        <Button icon={<UploadOutlined />} loading={busy}>
          {value ? "Replace image" : "Upload image"}
        </Button>
      </Upload>
    </Flex>
  );
}

interface ModalProps {
  product?: Product;
  onClose: () => void;
  onSaved: () => void;
}

function ProductModal({ product, onClose, onSaved }: ModalProps) {
  const [form] = Form.useForm<ProductInput>();
  const [busy, setBusy] = useState(false);
  const { message } = App.useApp();

  async function save(values: ProductInput) {
    setBusy(true);
    try {
      await (product ? api.updateProduct(product.id, values) : api.createProduct(values));
      onSaved();
    } catch (failure) {
      message.error(errorMessage(failure));
      setBusy(false);
    }
  }

  return (
    <Modal open title={product ? "Edit product" : "Create product"} okText={product ? "Save" : "Create"} confirmLoading={busy} onOk={form.submit} onCancel={onClose} destroyOnHidden>
      <Form form={form} layout="vertical" requiredMark={false} initialValues={product} onFinish={save}>
        <Form.Item name="name" label="Name" rules={[{ required: true, whitespace: true, max: 200, message: "Enter a name" }]}>
          <Input maxLength={200} placeholder="e.g. Wireless headphones" />
        </Form.Item>
        <Form.Item name="price" label="Price" rules={[{ required: true, message: "Enter a price" }]}>
          <InputNumber min={0.01} precision={2} suffix="UZS" style={{ width: "100%" }} />
        </Form.Item>
        <Form.Item name="image_url" label="Image" rules={[{ required: true, message: "Upload an image" }]}>
          <ImageUpload />
        </Form.Item>
      </Form>
    </Modal>
  );
}

export default function Products() {
  const { message } = App.useApp();
  const [products, setProducts] = useState<Product[] | null>(null);
  const [editing, setEditing] = useState<Product | "new" | null>(null);

  const load = useCallback(() => {
    api
      .products()
      .then(setProducts)
      .catch((error: Error) => {
        setProducts([]);
        message.error(error.message);
      });
  }, [message]);

  useEffect(load, [load]);

  async function remove(product: Product) {
    try {
      await api.deleteProduct(product.id);
      message.success("Product deleted");
      load();
    } catch (failure) {
      message.error(errorMessage(failure));
    }
  }

  const columns: TableColumnsType<Product> = [
    {
      title: "",
      dataIndex: "image_url",
      width: 72,
      render: (url: string) => <img src={url} alt="" width={48} height={48} style={{ objectFit: "contain" }} />,
    },
    { title: "Name", dataIndex: "name" },
    { title: "Price", dataIndex: "price", render: formatPrice },
    {
      title: "",
      align: "right",
      render: (_, product) => (
        <Flex justify="flex-end" gap={8}>
          <Button icon={<EditOutlined />} onClick={() => setEditing(product)}>
            Edit
          </Button>
          <Popconfirm title="Delete this product?" okText="Delete" okButtonProps={{ danger: true }} onConfirm={() => remove(product)}>
            <Button danger icon={<DeleteOutlined />}>
              Delete
            </Button>
          </Popconfirm>
        </Flex>
      ),
    },
  ];

  return (
    <Card
      title="Products"
      extra={
        <Button type="primary" icon={<PlusOutlined />} onClick={() => setEditing("new")}>
          Create product
        </Button>
      }
    >
      <Table
        rowKey="id"
        loading={products === null}
        dataSource={products ?? []}
        columns={columns}
        pagination={{ pageSize: 10, hideOnSinglePage: true }}
        scroll={{ x: "max-content" }}
        locale={{ emptyText: "There are no products yet" }}
      />
      {editing && (
        <ProductModal
          product={editing === "new" ? undefined : editing}
          onClose={() => setEditing(null)}
          onSaved={() => {
            setEditing(null);
            message.success("Product saved");
            load();
          }}
        />
      )}
    </Card>
  );
}
