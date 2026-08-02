import type { Metadata } from "next";
import { Mail, Clock } from "lucide-react";

import { product } from "@/lib/product";
import { SiteHeader } from "../_components/site-header";
import { SiteFooter } from "../_components/site-footer";

export const metadata: Metadata = {
  title: `Contact Us — ${product.name}`,
};

export default function ContactPage() {
  return (
    <>
      <SiteHeader />
      <main className="mx-auto max-w-3xl px-4 py-16 sm:px-6 lg:px-8">
        <h1 className="font-heading text-3xl font-extrabold tracking-tight text-foreground sm:text-4xl">
          Contact Us
        </h1>
        <p className="mt-4 max-w-xl text-lg leading-relaxed text-muted-foreground">
          Question about an order, a return, or the {product.name} itself?
          We&apos;re happy to help.
        </p>

        <div className="mt-10 grid gap-4 sm:grid-cols-2">
          <a
            href={`mailto:${product.supportEmail}`}
            className="flex items-start gap-4 rounded-2xl border border-border bg-card p-6 shadow-sm transition-shadow hover:shadow-md"
          >
            <span className="flex h-11 w-11 shrink-0 items-center justify-center rounded-xl bg-primary/10 text-primary">
              <Mail size={22} aria-hidden />
            </span>
            <div>
              <h2 className="font-heading text-lg font-bold text-foreground">
                Email
              </h2>
              <p className="mt-1 text-sm text-muted-foreground">
                {product.supportEmail}
              </p>
            </div>
          </a>

          <div className="flex items-start gap-4 rounded-2xl border border-border bg-card p-6 shadow-sm">
            <span className="flex h-11 w-11 shrink-0 items-center justify-center rounded-xl bg-primary/10 text-primary">
              <Clock size={22} aria-hidden />
            </span>
            <div>
              <h2 className="font-heading text-lg font-bold text-foreground">
                Response Time
              </h2>
              <p className="mt-1 text-sm text-muted-foreground">
                We reply to every email within 1–2 business days.
              </p>
            </div>
          </div>
        </div>

        <p className="mt-10 text-sm text-muted-foreground">
          For order or shipping questions, please include your order number
          so we can help faster.
        </p>
      </main>
      <SiteFooter />
    </>
  );
}
