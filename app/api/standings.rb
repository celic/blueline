module API
	class Standings < Grape::API

		desc 'Sort by'
		params do
			optional :division, type: Boolean, default: true
			optoonal :conference, type: Boolean, default: false
			optional :league, type: Boolean, default: false

			exactly_one_of :division, :conference, :league
		end

		get do

			# get teams
			teams = Team.all.order(:points)

		end
	end
end
