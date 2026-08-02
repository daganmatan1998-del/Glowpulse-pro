import Link from "next/link";
import { Coffee } from "lucide-react";

import { product } from "@/lib/product";

const columns = [
  {
    heading: "Shop",
    links: [
      { href: "/#features", label: "Features" },
      { href: "/#how-it-works", label: "How It Works" },
      { href: "/#reviews", label: "Reviews" },
      { href: "/#faq", label: "FAQ" },
    ],
  },
  {
    heading: "Support",
    links: [
      { href: "/shipping", label: "Shipping Policy" },
      { href: "/returns", label: "Return & Refund Policy" },
      { href: "/contact", label: "Contact Us" },
    ],
  },
  {
    heading: "Legal",
    links: [
      { href: "/privacy", label: "Privacy Policy" },
      { href: "/terms", label: "Terms of Service" },
    ],
  },
];

export function SiteFooter() {
  const year = new Date().getFullYear();

  return (
    <footer className="border-t border-border bg-background py-14">
      <div className="mx-auto max-w-6xl px-4 sm:px-6 lg:px-8">
        <div className="grid grid-cols-2 gap-8 sm:grid-cols-4">
          <div className="col-span-2 sm:col-span-1">
            <Link
              href="/"
              className="flex items-center gap-2 font-heading text-lg font-bold text-primary"
            >
              <span className="flex h-9 w-9 items-center justify-center rounded-full bg-primary text-primary-foreground">
                <Coffee size={18} aria-hidden />
              </span>
              {product.name}
            </Link>
            <p className="mt-3 text-sm text-muted-foreground">
              {product.tagline}
            </p>
          </div>

          {columns.map((col) => (
            <div key={col.heading}>
              <h3 className="text-sm font-semibold text-foreground">
                {col.heading}
              </h3>
              <ul className="mt-3 space-y-2">
                {col.links.map((link) => (
                  <li key={link.href}>
                    <Link
                      href={link.href}
                      className="text-sm text-muted-foreground transition-colors hover:text-foreground"
                    >
                      {link.label}
                    </Link>
                  </li>
                ))}
              </ul>
            </div>
          ))}
        </div>

        <div className="mt-10 border-t border-border pt-6 text-center text-sm text-muted-foreground">
          © {year} {product.name}. All rights reserved.
        </div>
      </div>
    </footer>
  );
}
