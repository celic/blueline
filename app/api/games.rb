module API
	class Games < Grape::API

		represent Game, with: API::Entities::Game

		resource :games do

			desc 'Search for games'
			params do
				optional :date, type: Date
				optional :team, type: String

				given :team do
					optional :opponent, type: String
					optional :home, type: Boolean
				end
			end
			get do

				# find games
				games = Game.all.order(date: :desc).limit(15)

				# filter by date if provided
				games = games.where date: params[:date] if params[:date]

				if params[:team]

					# find games with team matching criteria
					games =	if params[:home].nil?

								games.participant params[:team_id]

							elsif params[:home]

								games.where home_id: params[:team_id]

							else

								games.where away_id: params[:team_id]
							end

					# filter by opponent also if provided
					games = games.participant Team.by_abbrev(params[:opponent]) if params[:opponent]
				end

				# show games
				present :games, games
			end
		end
	end
end
