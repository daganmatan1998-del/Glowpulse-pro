import { ArrowRight } from "lucide-react";

import { buttonVariants } from "@/components/ui/button";
import { cn } from "@/lib/utils";
import { product } from "@/lib/product";

export function FinalCTA() {
  return (
    <section className="bg-primary py-20 text-primary-foreground">
      <div className="mx-auto max-w-3xl px-4 text-center sm:px-6 lg:px-8">
        <h2 className="font-heading text-3xl font-extrabold tracking-tight sm:text-4xl">
          Ready to Stop Stirring?
        </h2>
        <p className="mx-auto mt-4 max-w-xl text-primary-foreground/80">
          Join {product.soldCount}+ people who upgraded their morning coffee
          routine with the {product.name}.
        </p>
        <a
          href={product.buyUrl}
          target="_blank"
          rel="noopener noreferrer"
          className={cn(buttonVariants({ variant: "secondary", size: "lg" }), "mt-8")}
        >
          Shop Now
          <ArrowRight size={18} aria-hidden />
        </a>
        <p className="mt-4 text-sm text-primary-foreground/60">
          ${product.priceUSD} · free shipping
        </p>
      </div>
    </section>
  );
}
