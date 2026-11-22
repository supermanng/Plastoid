import { useParams } from 'react-router-dom';
import { AuthoringEditor } from '../../../../shared/components/course-authoring/AuthoringEditor';

/*
 * /tutor/courses/:courseId — lesson builder for the tutor's own courses. The builder itself (<AuthoringEditor>) is
 * shared with the organisation portal, which renders it for organisation courses (/sponsor/courses/:courseId).
 */
export default function CourseBuilder() {
  const { courseId } = useParams();
  return <AuthoringEditor courseId={courseId} basePath="/tutor/courses" listLabel="My courses" />;
}
