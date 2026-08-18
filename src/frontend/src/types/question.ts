
export type AnswerOption = {
    text: string;
    isCorrect: boolean;
    orderIndex: number;
};

export type Question = {
    id: string;
    text: string;
    questionType: number;
    timeLimitSeconds: number;
    points: number;
    options: AnswerOption[];
};