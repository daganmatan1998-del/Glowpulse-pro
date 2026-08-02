import type { Metadata } from "next";

import { product } from "@/lib/product";
import { PolicyLayout, PolicySection } from "../_components/policy-layout";

export const metadata: Metadata = {
  title: `Return & Refund Policy — ${product.name}`,
};

export default function ReturnsPolicyPage() {
  return (
    <PolicyLayout title="Return & Refund Policy" updated="August 2, 2026">
      <PolicySection title="30-Day Returns">
        <p>
          If you&apos;re not happy with your {product.name}, contact us
          within 30 days of delivery and we&apos;ll make it right — a
          replacement or a refund.
        </p>
      </PolicySection>

      <PolicySection title="How to Start a Return">
        <ol className="list-decimal space-y-2 pl-5">
          <li>
            Email{" "}
            <a
              href={`mailto:${product.supportEmail}`}
              className="font-medium text-primary underline underline-offset-2"
            >
              {product.supportEmail}
            </a>{" "}
            with your order number and the reason for the return.
          </li>
          <li>We&apos;ll get back to you within 2 business days with next steps.</li>
          <li>
            Because return shipping across borders often costs more than the
            item itself, for most issues we&apos;ll offer a replacement or a
            refund without asking you to ship anything back. For other
            returns, we&apos;ll let you know if return shipping is needed and
            who covers it.
          </li>
        </ol>
      </PolicySection>

      <PolicySection title="Damaged or Defective Items">
        <p>
          If your order arrives damaged or doesn&apos;t work as described,
          tell us within 30 days with a photo or video — we&apos;ll send a
          replacement or a full refund, no questions asked.
        </p>
      </PolicySection>

      <PolicySection title="Refunds">
        <p>
          Once a return or replacement is approved, refunds are issued to
          your original payment method within 5–10 business days.
        </p>
      </PolicySection>

      <PolicySection title="Questions?">
        <p>
          Email{" "}
          <a
            href={`mailto:${product.supportEmail}`}
            className="font-medium text-primary underline underline-offset-2"
          >
            {product.supportEmail}
          </a>{" "}
          — we&apos;re happy to help.
        </p>
      </PolicySection>
    </PolicyLayout>
  );
}
