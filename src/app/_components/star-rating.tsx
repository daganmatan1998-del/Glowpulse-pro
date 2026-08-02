import { Star } from "lucide-react";

export function StarRating({
  rating,
  size = 16,
}: {
  rating: number;
  size?: number;
}) {
  return (
    <div
      className="flex items-center gap-0.5"
      role="img"
      aria-label={`${rating} out of 5 stars`}
    >
      {[0, 1, 2, 3, 4].map((i) => {
        const filled = rating - i >= 0.75;
        const half = !filled && rating - i >= 0.25;
        return (
          <Star
            key={i}
            size={size}
            aria-hidden
            className={
              filled
                ? "fill-star text-star"
                : half
                  ? "fill-star/50 text-star"
                  : "fill-transparent text-border"
            }
          />
        );
      })}
    </div>
  );
}
