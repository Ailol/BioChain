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

    </Routes>
  );
}
