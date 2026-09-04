export type SurveyQuestionType = 'yesno' | 'singlechoice' | 'multichoice';

export interface CreateSurveyDto {
  title: string;
  questionText: string;
  questionType: SurveyQuestionType;
  options?: string[];
  targetUserIds: string[];
}

export interface SurveySummaryDto {
  id: string;
  title: string;
  questionText: string;
  questionType: SurveyQuestionType;
  options?: string[];
  createdAt: string;
  totalAssigned: number;
  totalAnswered: number;
  totalPending: number;
  totalExpired: number;
}

export interface SurveyIndividualResponseDto {
  assignmentId: number;
  userId: string;
  userName: string;
  userEmail: string;
  status: 'pending' | 'answered' | 'expired';
  answer?: string;
  answeredAt?: string;
  sentAt: string;
  expiresAt: string;
}

export interface SurveyResultDto {
  summary: SurveySummaryDto;
  aggregateCounts: Record<string, number>;
  responses: SurveyIndividualResponseDto[];
}

export interface SurveyAssignmentDto {
  assignmentId: number;
  surveyId: string;
  title: string;
  questionText: string;
  questionType: SurveyQuestionType;
  options?: string[];
  sentAt: string;
  expiresAt: string;
}

export interface AnswerSurveyDto {
  assignmentId: number;
  answer: string;
}

export interface ResendSurveyDto {
  assignmentIds?: number[];
}

export interface UserSurveyHistoryDto {
  assignmentId: number;
  surveyId: string;
  title: string;
  questionText: string;
  questionType: SurveyQuestionType;
  status: 'pending' | 'answered' | 'expired';
  answer?: string;
  sentAt: string;
  expiresAt: string;
  answeredAt?: string;
}
