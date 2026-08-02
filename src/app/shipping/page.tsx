import type { Metadata } from "next";

import { product } from "@/lib/product";
import { PolicyLayout, PolicySection } from "../_components/policy-layout";

export const metadata: Metadata = {
  title: `Shipping Policy — ${product.name}`,
};

export default function ShippingPolicyPage() {
  return (
    <PolicyLayout title="Shipping Policy" updated="August 2, 2026">
      <PolicySection title="Processing Time">
        <p>
          Orders are processed within 1–3 business days of payment
          confirmation. You&apos;ll receive an email once your order ships.
        </p>
      </PolicySection>

      <PolicySection title="Delivery Time">
        <p>
          Standard delivery typically takes 7–20 business days, depending on
          your location and local customs processing. We know that&apos;s
          longer than a same-day-delivery world — we&apos;d rather tell you
          upfront than promise something we can&apos;t keep.
        </p>
      </PolicySection>

      <PolicySection title="Shipping Cost">
        <p>Shipping is free on every order, worldwide.</p>
      </PolicySection>

      <PolicySection title="Tracking">
        <p>
          Once your order ships, we&apos;ll email you a tracking number so
          you can follow it the whole way.
        </p>
      </PolicySection>

      <PolicySection title="Customs & Import Duties">
        <p>
          For international orders, your country may charge customs fees or
          import duties on arrival. These are set by your local government
          and are the responsibility of the customer — they&apos;re not
          included in your order total.
        </p>
      </PolicySection>

      <PolicySection title="Questions?">
        <p>
          Email us at{" "}
          <a
            href={`mailto:${product.supportEmail}`}
            className="font-medium text-primary underline underline-offset-2"
          >
            {product.supportEmail}
          </a>{" "}
          and we&apos;ll help you track down your order.
        </p>
      </PolicySection>
    </PolicyLayout>
  );
}
