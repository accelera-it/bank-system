import type { ReactNode } from "react";
import { Link, NavLink } from "react-router-dom";

import type { User } from "../../types";

function Icon({ children }: { children: ReactNode }) {
  return (
    <svg className="h-4 w-4 shrink-0" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      {children}
    </svg>
  );
}

const HomeIcon = () => (
  <Icon>
    <path d="m3 11 9-8 9 8" />
    <path d="M5 9.5V21h14V9.5" />
  </Icon>
);

const ProductsIcon = () => (
  <Icon>
    <path d="M6 2 3 6v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2V6l-3-4Z" />
    <path d="M3 6h18" />
    <path d="M16 10a4 4 0 0 1-8 0" />
  </Icon>
);

const LoginIcon = () => (
  <Icon>
    <path d="M15 3h4a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2h-4" />
    <path d="m10 17 5-5-5-5" />
    <path d="M15 12H3" />
  </Icon>
);

const CabinetIcon = () => (
  <Icon>
    <rect x="3" y="3" width="7" height="9" rx="1" />
    <rect x="14" y="3" width="7" height="5" rx="1" />
    <rect x="14" y="12" width="7" height="9" rx="1" />
    <rect x="3" y="16" width="7" height="5" rx="1" />
  </Icon>
);

const MENU = [
  { to: "/", label: "Home", icon: <HomeIcon />, end: true },
  { to: "/products", label: "Products", icon: <ProductsIcon />, end: false },
];

export default function Header({ user }: { user: User | null }) {
  return (
    <header className="sticky top-0 z-10 border-b border-slate-200/80 bg-white/90 shadow-sm backdrop-blur">
      <div className="mx-auto grid h-16 max-w-6xl grid-cols-[1fr_auto_1fr] items-center gap-2 px-4 sm:gap-4">
        <Link to="/" className="justify-self-start">
          <img src="/img/brand.svg" alt="Shop" className="h-9 w-auto" />
        </Link>

        <nav aria-label="Main" className="flex items-center gap-1 rounded-full bg-slate-100 p-1">
          {MENU.map(({ to, label, icon, end }) => (
            <NavLink
              key={to}
              to={to}
              end={end}
              className={({ isActive }) =>
                `flex items-center gap-1.5 rounded-full px-3 py-1.5 text-sm font-medium transition sm:px-4 ${
                  isActive ? "bg-white text-brand-text shadow-sm" : "text-slate-600 hover:text-slate-900"
                }`
              }
            >
              <span className="max-sm:hidden">{icon}</span>
              {label}
            </NavLink>
          ))}
        </nav>

        <div className="justify-self-end">
          {user ? (
            <Link
              to="/cabinet"
              className="flex items-center gap-1.5 rounded-full bg-brand px-3 py-2 text-sm font-semibold text-slate-900 shadow-sm transition hover:bg-brand-dark sm:px-4"
            >
              <CabinetIcon />
              Cabinet
            </Link>
          ) : (
            <a
              href="/api/auth/login"
              className="flex items-center gap-1.5 rounded-full border border-slate-300 px-3 py-2 text-sm font-semibold text-slate-700 transition hover:border-brand-dark hover:text-brand-text sm:px-4"
            >
              <LoginIcon />
              Login
            </a>
          )}
        </div>
      </div>
    </header>
  );
}
