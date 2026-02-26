import { Routes, Route, Navigate } from 'react-router-dom';
import { Layout } from '@/components/Layout';
import { ProtectedRoute } from '@/auth/ProtectedRoute';
import LoginPage from '@/pages/LoginPage';
import RoleSelectionPage from '@/pages/RoleSelectionPage';
import UnauthorizedPage from '@/pages/UnauthorizedPage';
import BioSpherePage from '@/pages/personal/BioSpherePage';
import PersonalInsightPage from '@/pages/personal/PersonalInsightPage';
import AnalyzeDocumentPage from '@/pages/professional/AnalyzeDocumentPage';
import CandidatesPage from '@/pages/professional/CandidatesPage';
import ChatAnalysisPage from '@/pages/professional/ChatAnalysisPage';
import UserManagementPage from '@/pages/admin/UserManagementPage';
import SignalsPage from '@/pages/admin/SignalsPage';
import InteractionsPage from '@/pages/admin/InteractionsPage';
import DimensionsPage from '@/pages/admin/DimensionsPage';
import EmbeddingsPage from '@/pages/admin/EmbeddingsPage';
import QuestionnairePage from '@/pages/personal/QuestionnairePage';

export default function App() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route path="/select-role" element={<RoleSelectionPage />} />
      <Route path="/unauthorized" element={<UnauthorizedPage />} />

      {/* Public questionnaire (token-based, no auth, no layout) */}
      <Route path="/questionnaire/:token" element={<QuestionnairePage />} />

      <Route element={<Layout />}>
        {/* Personal */}
        <Route element={<ProtectedRoute requiredRoles={['private']} />}>
          <Route path="/personal/biosphere" element={<BioSpherePage />} />
          <Route path="/personal/insight" element={<PersonalInsightPage />} />
          <Route path="/personal/questionnaire" element={<QuestionnairePage />} />
        </Route>

        {/* Professional */}
        <Route element={<ProtectedRoute requiredRoles={['work']} />}>
          <Route path="/professional/analyze" element={<AnalyzeDocumentPage />} />
          <Route path="/professional/candidates" element={<CandidatesPage />} />
          <Route path="/professional/chat" element={<ChatAnalysisPage />} />
        </Route>

        {/* Admin */}
        <Route element={<ProtectedRoute requiredRoles={['admin']} />}>
          <Route path="/admin/users" element={<UserManagementPage />} />
          <Route path="/admin/signals" element={<SignalsPage />} />
          <Route path="/admin/interactions" element={<InteractionsPage />} />
          <Route path="/admin/dimensions" element={<DimensionsPage />} />
          <Route path="/admin/embeddings" element={<EmbeddingsPage />} />
        </Route>
      </Route>

      {/* Default redirect */}
      <Route path="/" element={<Navigate to="/personal/biosphere" replace />} />
      <Route path="*" element={<Navigate to="/personal/biosphere" replace />} />
    </Routes>
  );
}
