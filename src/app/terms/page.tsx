import type { Metadata } from "next";

import { product } from "@/lib/product";
import { PolicyLayout, PolicySection } from "../_components/policy-layout";

export const metadata: Metadata = {
  title: `Terms of Service — ${product.name}`,
};

export default function TermsPage() {
  return (
    <PolicyLayout title="Terms of Service" updated="August 2, 2026">
      <PolicySection title="Agreement">
        <p>
          By placing an order on this site, you agree to these terms. If you
          don&apos;t agree, please don&apos;t use the site to make a
          purchase.
        </p>
      </PolicySection>

      <PolicySection title="Products & Pricing">
        <p>
          We do our best to keep product descriptions, images, and prices
          accurate, but errors can happen. If we find a pricing or listing
          error after you&apos;ve ordered, we&apos;ll contact you before
          charging or shipping anything.
        </p>
      </PolicySection>

      <PolicySection title="Orders & Payment">
        <p>
          Payments are processed securely through PayPal. We reserve the
          right to cancel or refuse any order, for example in cases of
          suspected fraud or an out-of-stock item, in which case you&apos;ll
          receive a full refund.
        </p>
      </PolicySection>

      <PolicySection title="Shipping & Returns">
        <p>
          See our{" "}
          <a href="/shipping" className="font-medium text-primary underline underline-offset-2">
            Shipping Policy
          </a>{" "}
          and{" "}
          <a href="/returns" className="font-medium text-primary underline underline-offset-2">
            Return &amp; Refund Policy
          </a>{" "}
          for details on delivery times and returns.
        </p>
      </PolicySection>

      <PolicySection title="Limitation of Liability">
        <p>
          This site and its products are provided &quot;as is.&quot; To the
          extent permitted by law, we aren&apos;t liable for indirect or
          incidental damages arising from the use of this product or site.
        </p>
      </PolicySection>

      <PolicySection title="Changes to These Terms">
        <p>
          We may update these terms from time to time. Continued use of the
          site after changes means you accept the updated terms.
        </p>
      </PolicySection>

      <PolicySection title="Contact">
        <p>
          Questions about these terms? Email{" "}
          <a
            href={`mailto:${product.supportEmail}`}
            className="font-medium text-primary underline underline-offset-2"
          >
            {product.supportEmail}
          </a>
          .
        </p>
      </PolicySection>
    </PolicyLayout>
  );
}
