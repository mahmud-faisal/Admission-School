import { useNavigate } from 'react-router-dom';
import { CreateClassForm } from '../components/CreateClassForm';

export function CreateClassPage() {
  const navigate = useNavigate();
  return (
    <section>
      <h1>Create class</h1>
      <p className="muted">School admin: add a class (one shift) and its number of seats.</p>
      <CreateClassForm
        onCreated={(cls) =>
          navigate('/', { state: { flash: `Class “${cls.name}” was created with ${cls.totalSeats} seats.` } })
        }
      />
    </section>
  );
}
