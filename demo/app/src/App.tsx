import { lazy, Suspense } from "react";
import { Route, Routes } from "react-router-dom";

import SiteApp from "./site/SiteApp";

// Ant Design stays out of the shop's bundle.
const CabinetApp = lazy(() => import("./cabinet/CabinetApp"));

export default function App() {
  return (
    <Routes>
      <Route
        path="/cabinet/*"
        element={
          <Suspense>
            <CabinetApp />
          </Suspense>
        }
      />
      <Route path="*" element={<SiteApp />} />
    </Routes>
  );
}
