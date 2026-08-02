import { product } from "@/lib/product";
import { CheckoutButtons } from "./checkout-buttons";

export function FinalCTA() {
  return (
    <section id="checkout" className="bg-primary py-20 text-primary-foreground">
      <div className="mx-auto max-w-md px-4 text-center sm:px-6 lg:px-8">
        <h2 className="font-heading text-3xl font-extrabold tracking-tight sm:text-4xl">
          Ready to Stop Stirring?
        </h2>
        <p className="mx-auto mt-4 max-w-xl text-primary-foreground/80">
          Join {product.soldCount}+ people who upgraded their morning coffee
          routine with the {product.name}.
        </p>

        <div className="mt-8 rounded-2xl bg-card p-6 text-left shadow-xl">
          <div className="flex items-center justify-between">
            <span className="font-heading text-lg font-bold text-foreground">
              {product.name}
            </span>
            <span className="font-heading text-xl font-extrabold text-foreground">
              ${product.priceUSD}
            </span>
          </div>
          <p className="mt-1 text-sm text-muted-foreground">
            Free shipping · Secure checkout with PayPal
          </p>
          <div className="mt-5">
            <CheckoutButtons />
          </div>
        </div>
      </div>
    </section>
  );
}
