import { Compass } from 'lucide-react';
import { Button, EmptyState } from '../../components/ui';

export default function NotFound() {
  return (
    <div className="mx-auto max-w-lg px-4 py-16 sm:px-6">
      <EmptyState icon={Compass} title="Page not found" description="The link may be old or mistyped." action={<Button to="/">Back to home</Button>} />
    </div>
  );
}
