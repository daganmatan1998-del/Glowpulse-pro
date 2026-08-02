import { product } from "@/lib/product";

export function SiteFooter() {
  const year = new Date().getFullYear();

  return (
    <footer className="border-t border-border bg-background py-10">
      <div className="mx-auto max-w-6xl px-4 text-center text-sm text-muted-foreground sm:px-6 lg:px-8">
        <p>
          This is an independent product page, not an official AliExpress or{" "}
          {product.brand} site. It contains an affiliate link — purchases made
          through it may earn us a commission at no extra cost to you.
        </p>
        <p className="mt-2">
          © {year} {product.name}. All trademarks belong to their respective
          owners.
        </p>
      </div>
    </footer>
  );
}
