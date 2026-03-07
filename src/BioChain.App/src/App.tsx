import { Routes, Route, Navigate } from 'react-router-dom';
import { Layout } from '@/components/Layout';
import HereticChatPage from '@/pages/HereticChatPage';
import QuestionnairePage from '@/pages/QuestionnairePage';
import ConstellationPage from '@/pages/ConstellationPage';

export default function App() {
  return (
    <Routes>
      <Route
        path="/"
        element={
          <Layout>
            <HereticChatPage />
          </Layout>
        }
      />
      <Route
        path="/questionnaire"
        element={
          <Layout>
            <QuestionnairePage />
          </Layout>
        }
      />
      <Route
        path="/constellation"
        element={
          <Layout>
            <ConstellationPage />
          </Layout>
        }
      />
      <Route
        path="/constellation/:subjectId"
        element={
          <Layout>
            <ConstellationPage />
          </Layout>
        }
      />
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}
