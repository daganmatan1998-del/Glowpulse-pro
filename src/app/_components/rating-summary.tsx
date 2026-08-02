import { BadgeCheck } from "lucide-react";

import { product } from "@/lib/product";
import { StarRating } from "./star-rating";

export function RatingSummary() {
  return (
    <section id="reviews" className="mx-auto max-w-4xl px-4 py-20 text-center sm:px-6 lg:px-8">
      <div className="inline-flex items-center gap-2 rounded-full bg-secondary/15 px-4 py-1.5 text-sm font-semibold text-secondary">
        <BadgeCheck size={16} aria-hidden />
        AliExpress Choice Listing
      </div>

      <div className="mt-6 flex flex-col items-center gap-3">
        <span className="font-heading text-6xl font-extrabold text-foreground">
          {product.rating}
        </span>
        <StarRating rating={product.rating} size={26} />
        <p className="text-muted-foreground">
          Based on <strong className="text-foreground">{product.ratingCount} ratings</strong>{" "}
          · <strong className="text-foreground">{product.soldCount}+ sold</strong> on AliExpress
        </p>
      </div>

      <p className="mx-auto mt-8 max-w-xl text-sm text-muted-foreground">
        Ratings and sales figures are pulled directly from the live AliExpress
        listing linked throughout this page.
      </p>
    </section>
  );
}
