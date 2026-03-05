interface Props {
  text?: string;
  size?: 'sm' | 'md' | 'lg';
}

const sizes = {
  sm: 'w-4 h-4 border-2',
  md: 'w-8 h-8 border-2',
  lg: 'w-12 h-12 border-3',
};

export function LoadingSpinner({ text, size = 'md' }: Props) {
  return (
    <div className="flex flex-col items-center gap-3">
      <div
        className={`${sizes[size]} rounded-full border-bg-hover border-t-accent-primary animate-spin`}
      />
      {text && <p className="text-text-secondary text-sm">{text}</p>}
    </div>
  );
}
